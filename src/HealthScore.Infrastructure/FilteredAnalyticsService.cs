using HealthScore.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HealthScore.Infrastructure;

public sealed record AnalyticsFilter(string? Brand, string? Product, string? Scope, string? Issue)
{
    public string? Brand { get; } = Clean(Brand);
    public string? Product { get; } = Clean(Product);
    public string? Scope { get; } = Clean(Scope);
    public string? Issue { get; } = Issue is "with" or "without" ? Issue : null;
    public bool IsEmpty => Brand is null && Product is null && Scope is null && Issue is null;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record FilterOptions(IReadOnlyList<string> Brands, IReadOnlyList<string> Products, IReadOnlyList<string> Scopes);
public sealed record FilteredScoreRow(
    string EconomicGroup, int ActiveStores, int TotalCases, decimal Density, decimal AverageDensity,
    decimal DensityVsAverage, decimal SlaViolatedRate, decimal FcrRate, decimal IssueRate,
    decimal CriticalRate, decimal RecurrenceRate, decimal RecentGrowthRate,
    int DensityPoints, int GrowthPoints, int SlaPoints, int FcrPoints, int CriticalPoints,
    int IssuePoints, int RecurrencePoints, int Score, string RiskBand, string MainReason)
{
    public long Id { get; init; }
}

public sealed class FilteredAnalyticsService(HealthScoreDbContext db, IMemoryCache cache)
{
    public async Task<FilterOptions> GetOptionsAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue<FilterOptions>("analytics-filter-options", out var cached)) return cached!;
        var brands = await DistinctValues(db.Cases.Select(x => x.Brand), cancellationToken);
        var products = await DistinctValues(db.Cases.Select(x => x.Product), cancellationToken);
        var scopes = await DistinctValues(db.Cases.Select(x => x.OpeningVertical), cancellationToken);
        var result = new FilterOptions(brands, products, scopes);
        cache.Set("analytics-filter-options", result, TimeSpan.FromMinutes(30));
        return result;
    }

    public Task<IReadOnlyList<FilteredScoreRow>> CalculateAsync(
        DateOnly start, DateOnly end, AnalyticsFilter filter, ScoreConfiguration configuration, CancellationToken cancellationToken,
        TimeZoneInfo? timeZone = null)
    {
        var configurationHash = InitialScoreRules.AsJson(configuration).GetHashCode(StringComparison.Ordinal);
        var key = $"filtered-score:{start:yyyyMMdd}:{end:yyyyMMdd}:{timeZone?.Id}:{filter.Brand}:{filter.Product}:{filter.Scope}:{filter.Issue}:{configurationHash}";
        return cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            entry.Size = 1;
            return CalculateCoreAsync(start, end, filter, configuration, cancellationToken, timeZone);
        })!;
    }

    private async Task<IReadOnlyList<FilteredScoreRow>> CalculateCoreAsync(
        DateOnly start, DateOnly end, AnalyticsFilter filter, ScoreConfiguration configuration, CancellationToken cancellationToken,
        TimeZoneInfo? timeZone)
    {
        var startLocal = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var endLocal = DateTime.SpecifyKind(end.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var startUtc = timeZone is null ? DateTime.SpecifyKind(startLocal, DateTimeKind.Utc) : TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone);
        var endUtc = timeZone is null ? DateTime.SpecifyKind(endLocal, DateTimeKind.Utc) : TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone);
        var historyStart = startUtc.AddDays(-60);
        var businessDays = Math.Max(1, await db.BusinessCalendar.CountAsync(x => x.Date >= start && x.Date < end && x.IsBusinessDay, cancellationToken));

        var accountQuery = db.Accounts.AsNoTracking().Where(x => x.EconomicGroup != null && x.EconomicGroup != "" && x.Cnpj != null && x.Cnpj != "" && (x.Status == "ATIVO" || x.Status == "Ativa"));
        if (filter.Brand is not null) accountQuery = accountQuery.Where(x => x.Brand == filter.Brand);
        var stores = await accountQuery.GroupBy(x => x.EconomicGroup!).Select(group => new { Group = group.Key, Count = group.Select(x => x.Cnpj).Distinct().Count() })
            .ToDictionaryAsync(x => x.Group, x => x.Count, cancellationToken);

        var currentQuery = Apply(db.Cases.AsNoTracking().Where(x => x.SalesforceCreatedAt >= startUtc && x.SalesforceCreatedAt < endUtc && x.EconomicGroup != null && x.EconomicGroup != ""), filter);
        var priorities = configuration.CriticalPriorities.ToArray();
        var current = await currentQuery.GroupBy(x => x.EconomicGroup!).Select(group => new RawMetric(
            group.Key, group.Count(), group.Count(x => x.SlaViolated == true), group.Count(x => x.FirstContactResolution == true),
            group.Count(x => x.JiraIssueCode != null && x.JiraIssueCode != ""), group.Count(x => x.Priority != null && priorities.Contains(x.Priority))))
            .ToListAsync(cancellationToken);

        var historyQuery = Apply(db.Cases.AsNoTracking().Where(x => x.SalesforceCreatedAt >= historyStart && x.SalesforceCreatedAt < endUtc && x.EconomicGroup != null && x.EconomicGroup != ""), filter);
        var totals90 = await historyQuery.GroupBy(x => x.EconomicGroup!).Select(group => new { Group = group.Key, Total = group.Count() })
            .ToDictionaryAsync(x => x.Group, x => x.Total, cancellationToken);
        var recurrence = await RecurrenceAsync(Apply(db.Cases.AsNoTracking().Where(x => x.SalesforceCreatedAt >= startUtc.AddDays(-30) && x.SalesforceCreatedAt < endUtc && x.EconomicGroup != null && x.EconomicGroup != ""), filter), startUtc, cancellationToken);

        var eligible = current.Where(x => stores.GetValueOrDefault(x.Group) > 0).ToList();
        var densities = eligible.ToDictionary(x => x.Group, x => Divide(x.Total, stores[x.Group] * businessDays));
        var benchmark = densities.Count == 0 ? 0 : densities.Values.Average();
        return eligible.Select(metric => Build(metric, stores[metric.Group], businessDays, benchmark, densities[metric.Group], totals90.GetValueOrDefault(metric.Group), recurrence.GetValueOrDefault(metric.Group), configuration)).ToList();
    }

    private static IQueryable<CaseRecord> Apply(IQueryable<CaseRecord> query, AnalyticsFilter filter)
    {
        if (filter.Brand is not null) query = query.Where(x => x.Brand == filter.Brand);
        if (filter.Product is not null) query = query.Where(x => x.Product == filter.Product);
        if (filter.Scope is not null) query = query.Where(x => x.OpeningVertical == filter.Scope);
        if (filter.Issue == "with") query = query.Where(x => x.JiraIssueCode != null && x.JiraIssueCode != "");
        if (filter.Issue == "without") query = query.Where(x => x.JiraIssueCode == null || x.JiraIssueCode == "");
        return query;
    }

    private static FilteredScoreRow Build(RawMetric metric, int stores, int businessDays, decimal benchmark, decimal density, int historyTotal, int recurrenceCount, ScoreConfiguration config)
    {
        var densityRatio = Divide(density, benchmark);
        var sla = Divide(metric.Sla, metric.Total); var fcr = Divide(metric.Fcr, metric.Total);
        var issue = Divide(metric.Issue, metric.Total); var critical = Divide(metric.Critical, metric.Total);
        var recurrence = Divide(recurrenceCount, metric.Total);
        var monthlyAverage = historyTotal / 3m; var growth = monthlyAverage == 0 ? 0 : metric.Total / monthlyAverage - 1m;
        var densityPoints = InitialScoreRules.Scale(InitialScoreRules.DensityPoints(densityRatio), 25, config.Weights.Density);
        var growthPoints = InitialScoreRules.Scale(InitialScoreRules.GrowthPoints(growth), 15, config.Weights.Growth);
        var slaPoints = InitialScoreRules.Scale(InitialScoreRules.SlaPoints(sla), 15, config.Weights.Sla);
        var fcrPoints = InitialScoreRules.Scale(InitialScoreRules.FcrPoints(fcr), 10, config.Weights.Fcr);
        var criticalPoints = InitialScoreRules.Scale(InitialScoreRules.CriticalPoints(critical), 15, config.Weights.Criticality);
        var issuePoints = InitialScoreRules.Scale(InitialScoreRules.IssuePoints(issue), 10, config.Weights.Issue);
        var recurrencePoints = InitialScoreRules.Scale(InitialScoreRules.RecurrencePoints(recurrence), 10, config.Weights.Recurrence);
        var points = new Dictionary<string, int> { ["Densidade"] = densityPoints, ["Crescimento"] = growthPoints, ["SLA"] = slaPoints, ["FCR"] = fcrPoints, ["Criticidade"] = criticalPoints, ["Issue/JIRA"] = issuePoints, ["Recorrência"] = recurrencePoints };
        var score = Math.Min(100, points.Values.Sum());
        return new(metric.Group, stores, metric.Total, density, benchmark, densityRatio, sla, fcr, issue, critical, recurrence, growth,
            densityPoints, growthPoints, slaPoints, fcrPoints, criticalPoints, issuePoints, recurrencePoints,
            score, InitialScoreRules.RiskBand(score, config.Bands), InitialScoreRules.MainReason(points));
    }

    private static async Task<Dictionary<string, int>> RecurrenceAsync(IQueryable<CaseRecord> query, DateTime start, CancellationToken cancellationToken)
    {
        var rows = await query.Select(x => new { Group = x.EconomicGroup!, x.SalesforceCreatedAt, Theme = x.TaxonomyLevel4 ?? x.TaxonomyLevel3 ?? x.TaxonomyLevel2 ?? x.TaxonomyDescription })
            .Where(x => x.Theme != null && x.Theme != "").OrderBy(x => x.Group).ThenBy(x => x.Theme).ThenBy(x => x.SalesforceCreatedAt).ToListAsync(cancellationToken);
        var result = new Dictionary<string, int>(); string? previous = null; DateTime previousDate = default;
        foreach (var row in rows) { var key = row.Group + "\u001f" + row.Theme; if (row.SalesforceCreatedAt >= start && key == previous && row.SalesforceCreatedAt - previousDate <= TimeSpan.FromDays(30)) result[row.Group] = result.GetValueOrDefault(row.Group) + 1; previous = key; previousDate = row.SalesforceCreatedAt; }
        return result;
    }

    private static async Task<IReadOnlyList<string>> DistinctValues(IQueryable<string?> query, CancellationToken cancellationToken) =>
        await query.Where(x => x != null && x != "").Distinct().OrderBy(x => x).Select(x => x!).ToListAsync(cancellationToken);
    private static decimal Divide(decimal numerator, decimal denominator) => denominator == 0 ? 0 : numerator / denominator;
    private sealed record RawMetric(string Group, int Total, int Sla, int Fcr, int Issue, int Critical);
}
