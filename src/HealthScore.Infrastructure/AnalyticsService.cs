using HealthScore.Application;
using HealthScore.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HealthScore.Infrastructure;

public sealed class AnalyticsService(HealthScoreDbContext db, AccountGroupResolver groupResolver, ILogger<AnalyticsService> logger) : IAnalyticsService
{
    private static readonly HashSet<string> CriticalPriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Altíssima", "Alta", "High", "Disaster", "P0", "P1"
    };

    public async Task<AnalyticsSummary> RebuildAsync(CancellationToken cancellationToken)
    {
        await EnsureRuleVersionAsync(cancellationToken);
        var ruleVersion = await db.ScoreRuleVersions.AsNoTracking()
            .Where(x => x.Status == "published").OrderByDescending(x => x.Id).FirstAsync(cancellationToken);
        var configuration = InitialScoreRules.Parse(ruleVersion.ConfigurationJson);
        await groupResolver.ResolveAsync(cancellationToken);
        await EnsureCalendarAsync(cancellationToken);
        var groups = await RebuildGroupsAsync(cancellationToken);

        var end = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));
        var start = end.AddDays(-30);
        var snapshots = await CalculateSnapshotsAsync(start, end, "rolling30", ruleVersion.Id, configuration, cancellationToken);
        var currentMonth = new DateOnly(end.Year, end.Month, 1);
        for (var offset = 0; offset < 6; offset++)
        {
            var monthStart = currentMonth.AddMonths(-offset);
            var monthEnd = offset == 0 ? end : currentMonth.AddMonths(1 - offset);
            snapshots += await CalculateSnapshotsAsync(monthStart, monthEnd, "monthly", ruleVersion.Id, configuration, cancellationToken);
        }
        logger.LogInformation("Analytics rebuilt: {Groups} groups and {Snapshots} score snapshots", groups, snapshots);
        return new AnalyticsSummary(groups, snapshots, start, end);
    }

    private async Task EnsureRuleVersionAsync(CancellationToken cancellationToken)
    {
        if (await db.ScoreRuleVersions.AnyAsync(x => x.Id == InitialScoreRules.Version, cancellationToken)) return;
        db.ScoreRuleVersions.Add(new ScoreRuleVersion
        {
            Id = InitialScoreRules.Version,
            Name = "Regra inicial v1",
            Status = "published",
            ConfigurationJson = InitialScoreRules.AsJson(),
            CreatedBy = "system",
            Justification = "Configuração inicial baseada na especificação funcional.",
            CreatedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCalendarAsync(CancellationToken cancellationToken)
    {
        var first = new DateOnly(2022, 1, 1);
        var last = new DateOnly(DateTime.UtcNow.Year + 1, 12, 31);
        var existing = (await db.BusinessCalendar.Select(x => x.Date).ToListAsync(cancellationToken)).ToHashSet();
        var pending = new List<BusinessCalendarDay>();
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            if (existing.Contains(date)) continue;
            pending.Add(new BusinessCalendarDay
            {
                Date = date,
                IsBusinessDay = date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday
            });
        }
        db.BusinessCalendar.AddRange(pending);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> RebuildGroupsAsync(CancellationToken cancellationToken)
    {
        var activeStatuses = new[] { "ATIVO", "Ativa" };
        var rows = await db.Accounts.AsNoTracking()
            .Where(x => x.EconomicGroup != null && x.EconomicGroup != "" && x.Cnpj != null && x.Cnpj != "" && activeStatuses.Contains(x.Status!))
            .GroupBy(x => x.EconomicGroup!)
            .Select(group => new { Name = group.Key, Stores = group.Select(x => x.Cnpj).Distinct().Count() })
            .ToListAsync(cancellationToken);

        await db.EconomicGroups.ExecuteDeleteAsync(cancellationToken);
        var now = DateTime.UtcNow;
        db.EconomicGroups.AddRange(rows.Select(x => new EconomicGroup { Name = x.Name, ActiveStores = x.Stores, UpdatedAt = now }));
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return rows.Count;
    }

    private async Task<int> CalculateSnapshotsAsync(
        DateOnly start, DateOnly end, string snapshotKind, int ruleVersionId,
        ScoreConfiguration configuration, CancellationToken cancellationToken)
    {
        var startUtc = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(end.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var historyStartUtc = startUtc.AddDays(-60);
        var businessDays = await db.BusinessCalendar.CountAsync(x => x.Date >= start && x.Date < end && x.IsBusinessDay, cancellationToken);
        businessDays = Math.Max(businessDays, 1);

        var storeCounts = await db.EconomicGroups.AsNoTracking().ToDictionaryAsync(x => x.Name, x => x.ActiveStores, cancellationToken);
        var current = await db.Cases.AsNoTracking()
            .Where(x => x.SalesforceCreatedAt >= startUtc && x.SalesforceCreatedAt < endUtc && x.EconomicGroup != null && x.EconomicGroup != "")
            .GroupBy(x => x.EconomicGroup!)
            .Select(group => new RawMetric(
                group.Key,
                group.Count(),
                group.Count(x => x.SlaViolated == true),
                group.Count(x => x.FirstContactResolution == true),
                group.Count(x => x.JiraIssueCode != null && x.JiraIssueCode != ""),
                group.Count(x => x.Priority != null && configuration.CriticalPriorities.Contains(x.Priority))))
            .ToListAsync(cancellationToken);

        var totals90 = await db.Cases.AsNoTracking()
            .Where(x => x.SalesforceCreatedAt >= historyStartUtc && x.SalesforceCreatedAt < endUtc && x.EconomicGroup != null && x.EconomicGroup != "")
            .GroupBy(x => x.EconomicGroup!)
            .Select(group => new { Group = group.Key, Total = group.Count() })
            .ToDictionaryAsync(x => x.Group, x => x.Total, cancellationToken);

        var recurrenceCounts = await CalculateRecurrenceAsync(startUtc, endUtc, cancellationToken);
        var eligible = current.Where(x => storeCounts.TryGetValue(x.Group, out var stores) && stores > 0).ToList();
        var densityByGroup = eligible.ToDictionary(x => x.Group, x => Divide(x.Total, storeCounts[x.Group] * businessDays));
        var averageDensity = densityByGroup.Count == 0 ? 0 : densityByGroup.Values.Average();
        var calculatedAt = DateTime.UtcNow;
        var snapshots = new List<GroupScoreSnapshot>(eligible.Count);

        foreach (var metric in eligible)
        {
            var density = densityByGroup[metric.Group];
            var densityVsAverage = Divide(density, averageDensity);
            var sla = Divide(metric.SlaViolated, metric.Total);
            var fcr = Divide(metric.Fcr, metric.Total);
            var issue = Divide(metric.Issue, metric.Total);
            var critical = Divide(metric.Critical, metric.Total);
            var recurrence = Divide(recurrenceCounts.GetValueOrDefault(metric.Group), metric.Total);
            var monthlyAverage90 = totals90.GetValueOrDefault(metric.Group) / 3m;
            var growth = monthlyAverage90 == 0 ? 0 : metric.Total / monthlyAverage90 - 1m;
            var basePoints = new Dictionary<string, int>
            {
                ["Densidade"] = InitialScoreRules.DensityPoints(densityVsAverage),
                ["Crescimento"] = InitialScoreRules.GrowthPoints(growth),
                ["SLA"] = InitialScoreRules.SlaPoints(sla),
                ["FCR"] = InitialScoreRules.FcrPoints(fcr),
                ["Criticidade"] = InitialScoreRules.CriticalPoints(critical),
                ["Issue/JIRA"] = InitialScoreRules.IssuePoints(issue),
                ["Recorrência"] = InitialScoreRules.RecurrencePoints(recurrence)
            };
            var points = new Dictionary<string, int>
            {
                ["Densidade"] = InitialScoreRules.Scale(basePoints["Densidade"], 25, configuration.Weights.Density),
                ["Crescimento"] = InitialScoreRules.Scale(basePoints["Crescimento"], 15, configuration.Weights.Growth),
                ["SLA"] = InitialScoreRules.Scale(basePoints["SLA"], 15, configuration.Weights.Sla),
                ["FCR"] = InitialScoreRules.Scale(basePoints["FCR"], 10, configuration.Weights.Fcr),
                ["Criticidade"] = InitialScoreRules.Scale(basePoints["Criticidade"], 15, configuration.Weights.Criticality),
                ["Issue/JIRA"] = InitialScoreRules.Scale(basePoints["Issue/JIRA"], 10, configuration.Weights.Issue),
                ["Recorrência"] = InitialScoreRules.Scale(basePoints["Recorrência"], 10, configuration.Weights.Recurrence)
            };
            var score = Math.Min(points.Values.Sum(), 100);
            snapshots.Add(new GroupScoreSnapshot
            {
                EconomicGroup = metric.Group, SnapshotKind = snapshotKind, PeriodStart = start, PeriodEndExclusive = end,
                ScoreRuleVersionId = ruleVersionId, ActiveStores = storeCounts[metric.Group], BusinessDays = businessDays,
                TotalCases = metric.Total, Density = density, AverageDensity = averageDensity, DensityVsAverage = densityVsAverage,
                SlaViolatedRate = sla, FcrRate = fcr, IssueRate = issue, CriticalRate = critical,
                RecurrenceRate = recurrence, RecentGrowthRate = growth,
                DensityPoints = points["Densidade"], GrowthPoints = points["Crescimento"], SlaPoints = points["SLA"],
                FcrPoints = points["FCR"], CriticalPoints = points["Criticidade"], IssuePoints = points["Issue/JIRA"],
                RecurrencePoints = points["Recorrência"], Score = score, RiskBand = InitialScoreRules.RiskBand(score, configuration.Bands),
                MainReason = InitialScoreRules.MainReason(points), CalculatedAt = calculatedAt
            });
        }

        await db.GroupScoreSnapshots
            .Where(x => x.SnapshotKind == snapshotKind && x.PeriodStart == start && x.PeriodEndExclusive == end && x.ScoreRuleVersionId == ruleVersionId)
            .ExecuteDeleteAsync(cancellationToken);
        foreach (var batch in snapshots.Chunk(1000))
        {
            db.GroupScoreSnapshots.AddRange(batch);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
        return snapshots.Count;
    }

    private async Task<Dictionary<string, int>> CalculateRecurrenceAsync(DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var rows = await db.Cases.AsNoTracking()
            .Where(x => x.SalesforceCreatedAt >= start.AddDays(-30) && x.SalesforceCreatedAt < end && x.EconomicGroup != null && x.EconomicGroup != "")
            .Select(x => new
            {
                Group = x.EconomicGroup!, x.SalesforceCreatedAt,
                Theme = x.TaxonomyLevel4 ?? x.TaxonomyLevel3 ?? x.TaxonomyLevel2 ?? x.TaxonomyDescription
            })
            .Where(x => x.Theme != null && x.Theme != "")
            .OrderBy(x => x.Group).ThenBy(x => x.Theme).ThenBy(x => x.SalesforceCreatedAt)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, int>();
        string? previousKey = null;
        DateTime previousDate = default;
        foreach (var row in rows)
        {
            var key = row.Group + "\u001f" + row.Theme;
            if (row.SalesforceCreatedAt >= start && key == previousKey && row.SalesforceCreatedAt - previousDate <= TimeSpan.FromDays(30))
            {
                result[row.Group] = result.GetValueOrDefault(row.Group) + 1;
            }
            previousKey = key;
            previousDate = row.SalesforceCreatedAt;
        }
        return result;
    }

    private static decimal Divide(decimal numerator, decimal denominator) => denominator == 0 ? 0 : numerator / denominator;
    private sealed record RawMetric(string Group, int Total, int SlaViolated, int Fcr, int Issue, int Critical);
}
