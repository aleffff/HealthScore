using HealthScore.Application;
using HealthScore.Api;
using HealthScore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthScoreInfrastructure(builder.Configuration);
builder.Services.AddHealthScoreSecurity(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new UtcDateTimeJsonConverter()));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapGet("/health/ready", async (HealthScoreDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.Problem("MariaDB is unavailable", statusCode: 503)).AllowAnonymous();

app.MapGet("/api/v1/auth/config", (IConfiguration configuration) =>
{
    var auth = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
    return Results.Ok(new { mode = auth.Mode.ToLowerInvariant(), authority = auth.Authority, clientId = auth.ClientId, scope = auth.Scope });
}).AllowAnonymous();

app.MapGet("/api/v1/session", (ClaimsPrincipal user) => Results.Ok(new
{
    user = user.Identity?.Name,
    roles = new[] { "Viewer", "Operator", "ScoreAdmin", "SystemAdmin" }.Where(user.IsInRole)
})).RequireAuthorization("Viewer");

app.MapGet("/api/v1/sync/status", async (HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    var accounts = await db.Accounts.AsNoTracking().CountAsync(cancellationToken);
    var cases = await db.Cases.AsNoTracking().CountAsync(cancellationToken);
    var watermarks = await db.SyncWatermarks.AsNoTracking().OrderBy(x => x.EntityName).ToListAsync(cancellationToken);
    var runs = await db.SyncRuns.AsNoTracking().OrderByDescending(x => x.StartedAt).Take(20).ToListAsync(cancellationToken);
    return Results.Ok(new { vertical = "FARMA", accounts, cases, watermarks, runs });
}).RequireAuthorization("SystemAdmin");

app.MapGet("/api/v1/operations/overview", async (HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    var accounts = await db.Accounts.AsNoTracking().CountAsync(cancellationToken);
    var cases = await db.Cases.AsNoTracking().CountAsync(cancellationToken);
    var accountsWithoutGroup = await db.Accounts.AsNoTracking().CountAsync(x => x.EconomicGroup == null || x.EconomicGroup == "", cancellationToken);
    var accountsWithoutCnpj = await db.Accounts.AsNoTracking().CountAsync(x => x.Cnpj == null || x.Cnpj == "", cancellationToken);
    var accountsWithParent = await db.Accounts.AsNoTracking().CountAsync(x => x.ParentSalesforceId != null && x.ParentSalesforceId != "", cancellationToken);
    var accountsWithValidCnpjRoot = await db.Accounts.AsNoTracking().CountAsync(x => x.CnpjRoot != null && x.CnpjRoot != "", cancellationToken);
    var resolvedGroups = await db.Accounts.AsNoTracking().Select(x => x.EconomicGroup).Distinct().CountAsync(cancellationToken);
    var casesWithoutGroup = await db.Cases.AsNoTracking().CountAsync(x => x.EconomicGroup == null || x.EconomicGroup == "", cancellationToken);
    var lastRuns = await db.SyncRuns.AsNoTracking().GroupBy(x => x.EntityName)
        .Select(group => group.OrderByDescending(x => x.StartedAt).First())
        .ToListAsync(cancellationToken);
    var lastSnapshot = await db.GroupScoreSnapshots.AsNoTracking().Where(x => x.SnapshotKind == "rolling30")
        .MaxAsync(x => (DateTime?)x.CalculatedAt, cancellationToken);
    var snapshotGroups = lastSnapshot.HasValue
        ? await db.GroupScoreSnapshots.AsNoTracking().CountAsync(x => x.SnapshotKind == "rolling30" && x.CalculatedAt == lastSnapshot.Value, cancellationToken)
        : 0;
    var activeRule = await db.ScoreRuleVersions.AsNoTracking().Where(x => x.Status == "published")
        .OrderByDescending(x => x.Id).Select(x => new { version = x.Id, x.Name, x.PublishedAt, x.CreatedBy }).FirstAsync(cancellationToken);
    var actionPlans = await db.ActionPlans.AsNoTracking().GroupBy(x => x.Status)
        .Select(group => new { status = group.Key, total = group.Count() }).ToListAsync(cancellationToken);
    return Results.Ok(new
    {
        generatedAt = DateTime.UtcNow,
        ingestion = new { accounts, cases, lastRuns },
        quality = new
        {
            accountsWithoutGroup, accountsWithoutCnpj, casesWithoutGroup, accountsWithParent, accountsWithValidCnpjRoot, resolvedGroups,
            accountsWithoutGroupRate = accounts == 0 ? 0 : (decimal)accountsWithoutGroup / accounts,
            accountsWithoutCnpjRate = accounts == 0 ? 0 : (decimal)accountsWithoutCnpj / accounts,
            casesWithoutGroupRate = cases == 0 ? 0 : (decimal)casesWithoutGroup / cases
        },
        analytics = new { lastSnapshot, snapshotGroups, activeRule },
        actionPlans
    });
}).RequireAuthorization("SystemAdmin");

app.MapGet("/api/v1/data/accounts", async (int? page, int? pageSize, HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    var safePage = Math.Max(page ?? 1, 1);
    var safePageSize = Math.Clamp(pageSize ?? 50, 1, 200);
    var query = db.Accounts.AsNoTracking().OrderBy(x => x.Name);
    var total = await query.CountAsync(cancellationToken);
    var items = await query.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToListAsync(cancellationToken);
    return Results.Ok(new { page = safePage, pageSize = safePageSize, total, items });
}).RequireAuthorization("SystemAdmin");

app.MapGet("/api/v1/data/cases", async (DateTime? from, DateTime? to, int? page, int? pageSize, HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    var safePage = Math.Max(page ?? 1, 1);
    var safePageSize = Math.Clamp(pageSize ?? 50, 1, 200);
    var query = db.Cases.AsNoTracking().AsQueryable();
    if (from.HasValue) query = query.Where(x => x.SalesforceCreatedAt >= from.Value.ToUniversalTime());
    if (to.HasValue) query = query.Where(x => x.SalesforceCreatedAt < to.Value.ToUniversalTime());
    query = query.OrderByDescending(x => x.SalesforceCreatedAt);
    var total = await query.CountAsync(cancellationToken);
    var items = await query.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToListAsync(cancellationToken);
    return Results.Ok(new { page = safePage, pageSize = safePageSize, total, items });
}).RequireAuthorization("SystemAdmin");

app.MapGet("/api/v1/risk-score/periods", async (HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    var activeRuleId = await db.ScoreRuleVersions.AsNoTracking().Where(x => x.Status == "published").MaxAsync(x => (int?)x.Id, cancellationToken);
    var periods = await db.GroupScoreSnapshots.AsNoTracking()
        .Where(x => x.ScoreRuleVersionId == activeRuleId)
        .GroupBy(x => new { x.SnapshotKind, x.PeriodStart, x.PeriodEndExclusive })
        .Select(group => new { group.Key.SnapshotKind, group.Key.PeriodStart, group.Key.PeriodEndExclusive, Groups = group.Count() })
        .OrderByDescending(x => x.PeriodEndExclusive).ThenBy(x => x.SnapshotKind)
        .ToListAsync(cancellationToken);
    return Results.Ok(periods);
});

app.MapGet("/api/v1/risk-score/filters", async (FilteredAnalyticsService analytics, CancellationToken cancellationToken) =>
    Results.Ok(await analytics.GetOptionsAsync(cancellationToken))).RequireAuthorization("Viewer");

app.MapGet("/api/v1/risk-score/analysis", async (
    string? brand, string? product, string? scope, string? businessUnit, string? issue, string? riskBand, string? search,
    string? range, string? snapshotKind, DateOnly? periodStart, int? page, int? pageSize,
    HealthScoreDbContext db, FilteredAnalyticsService analytics, CancellationToken cancellationToken) =>
{
    var period = await ResolvePeriodAsync(db, range, snapshotKind, periodStart, cancellationToken);
    if (period is null) return Results.Ok(new { available = false, items = Array.Empty<object>() });
    var configuration = InitialScoreRules.Parse(period.Value.Rule.ConfigurationJson);
    var calculated = await analytics.CalculateAsync(period.Value.Start, period.Value.End, new AnalyticsFilter(brand, product, scope, businessUnit, issue), configuration, cancellationToken, period.Value.TimeZone);
    var idQuery = db.GroupScoreSnapshots.AsNoTracking().Where(x => x.ScoreRuleVersionId == period.Value.Rule.Id);
    idQuery = period.Value.Dynamic
        ? idQuery.Where(x => x.SnapshotKind == "rolling30")
        : idQuery.Where(x => x.SnapshotKind == period.Value.Kind && x.PeriodStart == period.Value.Start && x.PeriodEndExclusive == period.Value.End);
    var idRows = await idQuery.OrderByDescending(x => x.PeriodEndExclusive).Select(x => new { x.EconomicGroup, x.Id }).ToListAsync(cancellationToken);
    var snapshotIds = idRows.GroupBy(x => x.EconomicGroup).ToDictionary(x => x.Key, x => x.First().Id);
    var rows = calculated.Select(x => x with { Id = snapshotIds.GetValueOrDefault(x.EconomicGroup) }).ToList();
    var filtered = rows.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(riskBand)) filtered = filtered.Where(x => x.RiskBand == riskBand);
    if (!string.IsNullOrWhiteSpace(search)) filtered = filtered.Where(x => x.EconomicGroup.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
    var ranked = filtered.OrderByDescending(x => x.Score).ThenByDescending(x => x.TotalCases).ThenBy(x => x.EconomicGroup).ToList();
    var safePage = Math.Max(page ?? 1, 1); var safePageSize = Math.Clamp(pageSize ?? 50, 1, 200);
    var totalCases = rows.Sum(x => x.TotalCases); var criticalCases = rows.Where(x => x.RiskBand == "Crítico").Sum(x => x.TotalCases);
    return Results.Ok(new
    {
        available = true, snapshotKind = period.Value.Kind, periodStart = period.Value.Start, periodEndExclusive = period.Value.End,
        weights = configuration.Weights,
        total = ranked.Count, page = safePage, pageSize = safePageSize, items = ranked.Skip((safePage - 1) * safePageSize).Take(safePageSize),
        summary = new { available = true, snapshotKind = period.Value.Kind, periodStart = period.Value.Start, periodEndExclusive = period.Value.End,
            totalGroups = rows.Count, criticalGroups = rows.Count(x => x.RiskBand == "Crítico"), highGroups = rows.Count(x => x.RiskBand == "Alto"),
            averageScore = rows.Count == 0 ? 0 : rows.Average(x => (decimal)x.Score), totalCases, criticalCases,
            criticalCaseShare = totalCases == 0 ? 0 : (decimal)criticalCases / totalCases }
    });
}).RequireAuthorization("Viewer");

app.MapGet("/api/v1/risk-score/analysis/export", async (
    string? brand, string? product, string? scope, string? businessUnit, string? issue, string? riskBand, string? search,
    string? range, string? snapshotKind, DateOnly? periodStart, HealthScoreDbContext db, FilteredAnalyticsService analytics, CancellationToken cancellationToken) =>
{
    var period = await ResolvePeriodAsync(db, range, snapshotKind, periodStart, cancellationToken);
    if (period is null) return Results.NotFound();
    var rows = await analytics.CalculateAsync(period.Value.Start, period.Value.End, new AnalyticsFilter(brand, product, scope, businessUnit, issue), InitialScoreRules.Parse(period.Value.Rule.ConfigurationJson), cancellationToken, period.Value.TimeZone);
    var filtered = rows.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(riskBand)) filtered = filtered.Where(x => x.RiskBand == riskBand);
    if (!string.IsNullOrWhiteSpace(search)) filtered = filtered.Where(x => x.EconomicGroup.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
    var csv = new StringBuilder("Grupo Econômico;Lojas Ativas;Chamados;Score;Faixa;Principal Motivo;Densidade vs Média;SLA Violado;FCR;Issue;Criticidade;Recorrência;Crescimento\r\n");
    foreach (var row in filtered.OrderByDescending(x => x.Score).ThenBy(x => x.EconomicGroup))
        csv.AppendJoin(';', Csv(DisplayGroup(row.EconomicGroup)), row.ActiveStores, row.TotalCases, row.Score, Csv(row.RiskBand), Csv(row.MainReason), Csv(row.DensityVsAverage), Csv(row.SlaViolatedRate), Csv(row.FcrRate), Csv(row.IssueRate), Csv(row.CriticalRate), Csv(row.RecurrenceRate), Csv(row.RecentGrowthRate)).Append("\r\n");
    return Results.File(new UTF8Encoding(true).GetBytes(csv.ToString()), "text/csv; charset=utf-8", $"healthscore-farma-filtrado-{period.Value.Start:yyyy-MM-dd}.csv");
}).RequireAuthorization("Viewer");

app.MapGet("/api/v1/risk-score/summary", async (
    string? snapshotKind, DateOnly? periodStart, HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    snapshotKind = NormalizeSnapshotKind(snapshotKind);
    var activeRuleId = await db.ScoreRuleVersions.AsNoTracking().Where(x => x.Status == "published").MaxAsync(x => (int?)x.Id, cancellationToken);
    var periodQuery = db.GroupScoreSnapshots.AsNoTracking().Where(x => x.SnapshotKind == snapshotKind && x.ScoreRuleVersionId == activeRuleId);
    if (periodStart.HasValue) periodQuery = periodQuery.Where(x => x.PeriodStart == periodStart.Value);
    var period = await periodQuery.MaxAsync(x => (DateOnly?)x.PeriodEndExclusive, cancellationToken);
    if (!period.HasValue) return Results.Ok(new { available = false, vertical = "FARMA" });
    var query = periodQuery.Where(x => x.PeriodEndExclusive == period.Value);
    var resolvedStart = await query.MinAsync(x => x.PeriodStart, cancellationToken);
    var totalGroups = await query.CountAsync(cancellationToken);
    var criticalGroups = await query.CountAsync(x => x.RiskBand == "Crítico", cancellationToken);
    var highGroups = await query.CountAsync(x => x.RiskBand == "Alto", cancellationToken);
    var averageScore = await query.AverageAsync(x => (decimal)x.Score, cancellationToken);
    var totalCases = await query.SumAsync(x => x.TotalCases, cancellationToken);
    var criticalCases = await query.Where(x => x.RiskBand == "Crítico").SumAsync(x => x.TotalCases, cancellationToken);
    return Results.Ok(new
    {
        available = true, vertical = "FARMA", snapshotKind, periodStart = resolvedStart, periodEndExclusive = period, totalGroups, criticalGroups, highGroups,
        averageScore, totalCases, criticalCases, criticalCaseShare = totalCases == 0 ? 0 : (decimal)criticalCases / totalCases
    });
});

app.MapGet("/api/v1/risk-score/groups", async (
    string? riskBand, string? search, string? snapshotKind, DateOnly? periodStart,
    int? page, int? pageSize, HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    snapshotKind = NormalizeSnapshotKind(snapshotKind);
    var activeRuleId = await db.ScoreRuleVersions.AsNoTracking().Where(x => x.Status == "published").MaxAsync(x => (int?)x.Id, cancellationToken);
    var periodQuery = db.GroupScoreSnapshots.AsNoTracking().Where(x => x.SnapshotKind == snapshotKind && x.ScoreRuleVersionId == activeRuleId);
    if (periodStart.HasValue) periodQuery = periodQuery.Where(x => x.PeriodStart == periodStart.Value);
    var period = await periodQuery.MaxAsync(x => (DateOnly?)x.PeriodEndExclusive, cancellationToken);
    if (!period.HasValue) return Results.Ok(new { available = false, items = Array.Empty<object>() });
    var safePage = Math.Max(page ?? 1, 1);
    var safePageSize = Math.Clamp(pageSize ?? 50, 1, 200);
    var query = periodQuery.Where(x => x.PeriodEndExclusive == period.Value);
    if (!string.IsNullOrWhiteSpace(riskBand)) query = query.Where(x => x.RiskBand == riskBand);
    if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.EconomicGroup.Contains(search));
    var total = await query.CountAsync(cancellationToken);
    var items = await query.OrderByDescending(x => x.Score).ThenByDescending(x => x.TotalCases).ThenBy(x => x.EconomicGroup)
        .Skip((safePage - 1) * safePageSize).Take(safePageSize)
        .Select(x => new
        {
            x.Id, x.EconomicGroup, x.ActiveStores, x.TotalCases, x.Density, x.AverageDensity, x.DensityVsAverage,
            x.SlaViolatedRate, x.FcrRate, x.IssueRate, x.CriticalRate, x.RecurrenceRate, x.RecentGrowthRate,
            x.Score, x.RiskBand, x.MainReason, x.PeriodStart, x.PeriodEndExclusive, x.CalculatedAt
        }).ToListAsync(cancellationToken);
    return Results.Ok(new { available = true, page = safePage, pageSize = safePageSize, total, items });
});

app.MapGet("/api/v1/risk-score/groups/export", async (
    string? riskBand, string? search, string? snapshotKind, DateOnly? periodStart,
    HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    snapshotKind = NormalizeSnapshotKind(snapshotKind);
    var activeRuleId = await db.ScoreRuleVersions.AsNoTracking().Where(x => x.Status == "published").MaxAsync(x => (int?)x.Id, cancellationToken);
    var periodQuery = db.GroupScoreSnapshots.AsNoTracking().Where(x => x.SnapshotKind == snapshotKind && x.ScoreRuleVersionId == activeRuleId);
    if (periodStart.HasValue) periodQuery = periodQuery.Where(x => x.PeriodStart == periodStart.Value);
    var end = await periodQuery.MaxAsync(x => (DateOnly?)x.PeriodEndExclusive, cancellationToken);
    if (!end.HasValue) return Results.NotFound();
    var query = periodQuery.Where(x => x.PeriodEndExclusive == end.Value);
    if (!string.IsNullOrWhiteSpace(riskBand)) query = query.Where(x => x.RiskBand == riskBand);
    if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.EconomicGroup.Contains(search));
    var rows = await query.OrderByDescending(x => x.Score).ThenBy(x => x.EconomicGroup).ToListAsync(cancellationToken);
    var csv = new StringBuilder("Grupo Econômico;Lojas Ativas;Chamados;Score;Faixa;Principal Motivo;Densidade vs Média;SLA Violado;FCR;Issue;Criticidade;Recorrência;Crescimento\r\n");
    foreach (var row in rows)
    {
        csv.AppendJoin(';', Csv(DisplayGroup(row.EconomicGroup)), row.ActiveStores, row.TotalCases, row.Score, Csv(row.RiskBand), Csv(row.MainReason),
            Csv(row.DensityVsAverage), Csv(row.SlaViolatedRate), Csv(row.FcrRate), Csv(row.IssueRate),
            Csv(row.CriticalRate), Csv(row.RecurrenceRate), Csv(row.RecentGrowthRate)).Append("\r\n");
    }
    var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv.ToString());
    var filePeriod = (periodStart ?? end.Value).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    return Results.File(bytes, "text/csv; charset=utf-8", $"healthscore-farma-{filePeriod}.csv");
}).RequireAuthorization("Viewer");

app.MapGet("/api/v1/risk-score/groups/{id:long}", async (long id, HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    var snapshot = await db.GroupScoreSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (snapshot is null) return Results.NotFound();
    var rule = await db.ScoreRuleVersions.AsNoTracking().SingleAsync(x => x.Id == snapshot.ScoreRuleVersionId, cancellationToken);
    var config = InitialScoreRules.Parse(rule.ConfigurationJson);
    return Results.Ok(new
    {
        snapshot.Id, snapshot.EconomicGroup, snapshot.PeriodStart, snapshot.PeriodEndExclusive,
        metrics = new
        {
            snapshot.ActiveStores, snapshot.BusinessDays, snapshot.TotalCases, snapshot.Density, snapshot.AverageDensity,
            snapshot.DensityVsAverage, snapshot.SlaViolatedRate, snapshot.FcrRate, snapshot.IssueRate,
            snapshot.CriticalRate, snapshot.RecurrenceRate, snapshot.RecentGrowthRate
        },
        factors = new[]
        {
            new { name = "Densidade", points = snapshot.DensityPoints, maximum = config.Weights.Density },
            new { name = "Crescimento", points = snapshot.GrowthPoints, maximum = config.Weights.Growth },
            new { name = "SLA", points = snapshot.SlaPoints, maximum = config.Weights.Sla },
            new { name = "FCR", points = snapshot.FcrPoints, maximum = config.Weights.Fcr },
            new { name = "Criticidade", points = snapshot.CriticalPoints, maximum = config.Weights.Criticality },
            new { name = "Issue/JIRA", points = snapshot.IssuePoints, maximum = config.Weights.Issue },
            new { name = "Recorrência", points = snapshot.RecurrencePoints, maximum = config.Weights.Recurrence }
        },
        snapshot.Score, snapshot.RiskBand, snapshot.MainReason, suggestedAction = SuggestedAction(snapshot),
        snapshot.ScoreRuleVersionId, snapshot.CalculatedAt
    });
});

app.MapGet("/api/v1/audit/groups/{id:long}", async (long id, HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    var snapshot = await db.GroupScoreSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (snapshot is null) return Results.NotFound();
    var rule = await db.ScoreRuleVersions.AsNoTracking().SingleAsync(x => x.Id == snapshot.ScoreRuleVersionId, cancellationToken);
    var configuration = InitialScoreRules.Parse(rule.ConfigurationJson);
    var start = DateTime.SpecifyKind(snapshot.PeriodStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    var end = DateTime.SpecifyKind(snapshot.PeriodEndExclusive.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    var accounts = await db.Accounts.AsNoTracking().Where(x => x.EconomicGroup == snapshot.EconomicGroup)
        .OrderBy(x => x.Name).Select(x => new
        {
            x.SalesforceId, x.Name, x.Cnpj, x.CnpjRoot, x.ParentSalesforceId, x.ParentName,
            x.ReportedEconomicGroup, ResolvedEconomicGroup = x.EconomicGroup, x.Brand, x.Status
        }).ToListAsync(cancellationToken);
    var parentCounts = accounts.Where(x => x.ParentSalesforceId != null).GroupBy(x => x.ParentSalesforceId!).ToDictionary(x => x.Key, x => x.Count());
    var rootCounts = accounts.Where(x => x.CnpjRoot != null).GroupBy(x => x.CnpjRoot!).ToDictionary(x => x.Key, x => x.Count());
    var accountRows = accounts.Select(account => new
    {
        account.SalesforceId, account.Name, account.Cnpj, account.CnpjRoot, account.ParentSalesforceId, account.ParentName,
        account.ReportedEconomicGroup, account.ResolvedEconomicGroup, account.Brand, account.Status,
        activeStore = account.Cnpj != null && (account.Status == "ATIVO" || account.Status == "Ativa"),
        evidence = new[]
        {
            account.ParentSalesforceId is not null && parentCounts.GetValueOrDefault(account.ParentSalesforceId) > 1 ? "conta_pai" : null,
            account.CnpjRoot is not null && rootCounts.GetValueOrDefault(account.CnpjRoot) > 1 ? "raiz_cnpj" : null
        }.Where(x => x is not null)
    }).ToList();

    var cases = db.Cases.AsNoTracking().Where(x => x.EconomicGroup == snapshot.EconomicGroup && x.SalesforceCreatedAt >= start && x.SalesforceCreatedAt < end);
    var caseQuality = await cases.GroupBy(_ => 1).Select(group => new
    {
        total = group.Count(), slaMissing = group.Count(x => x.SlaViolated == null), fcrMissing = group.Count(x => x.FirstContactResolution == null),
        taxonomyMissing = group.Count(x => (x.TaxonomyLevel4 ?? x.TaxonomyLevel3 ?? x.TaxonomyLevel2 ?? x.TaxonomyDescription) == null),
        accountMissing = group.Count(x => x.AccountSalesforceId == null), closedBeforeCreated = group.Count(x => x.ClosedAt != null && x.ClosedAt < x.SalesforceCreatedAt),
        jira = group.Count(x => x.JiraIssueCode != null && x.JiraIssueCode != ""), sla = group.Count(x => x.SlaViolated == true),
        fcr = group.Count(x => x.FirstContactResolution == true), critical = group.Count(x => x.Priority != null && configuration.CriticalPriorities.Contains(x.Priority))
    }).FirstOrDefaultAsync(cancellationToken);
    var historicalTotal = await db.Cases.AsNoTracking().CountAsync(x => x.EconomicGroup == snapshot.EconomicGroup && x.SalesforceCreatedAt >= start.AddDays(-60) && x.SalesforceCreatedAt < end, cancellationToken);
    var reportedGroups = accounts.Select(x => x.ReportedEconomicGroup).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    var parentIds = accounts.Select(x => x.ParentSalesforceId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    var invalidCnpj = accounts.Count(x => x.Cnpj != null && x.CnpjRoot == null);
    var missingCnpj = accounts.Count(x => x.Cnpj == null);
    var anomalies = new List<object>();
    AddAnomaly(anomalies, "warning", "Contas sem CNPJ", missingCnpj, "Não participam da ligação por raiz de CNPJ nem da contagem de lojas ativas.");
    AddAnomaly(anomalies, "warning", "CNPJs inválidos", invalidCnpj, "Foram ignorados na formação do grupo porque os dígitos verificadores são inválidos.");
    AddAnomaly(anomalies, "info", "Mais de uma conta pai no componente", Math.Max(parentIds.Length - 1, 0), "Pode ser resultado de uma ligação transitiva pela mesma raiz de CNPJ; confira as lojas.");
    AddAnomaly(anomalies, "info", "Grupos reportados divergentes", Math.Max(reportedGroups.Length - 1, 0), "O agrupamento resolvido prevalece, mas os valores originais do Salesforce divergem.");
    AddAnomaly(anomalies, "warning", "Chamados sem SLA", caseQuality?.slaMissing ?? 0, "Reduz a base observável do indicador de SLA.");
    AddAnomaly(anomalies, "warning", "Chamados sem FCR", caseQuality?.fcrMissing ?? 0, "Reduz a base observável do indicador de FCR.");
    AddAnomaly(anomalies, "warning", "Chamados sem taxonomia", caseQuality?.taxonomyMissing ?? 0, "Prejudica a conferência de recorrência e principais ofensores.");
    AddAnomaly(anomalies, "danger", "Fechamento anterior à criação", caseQuality?.closedBeforeCreated ?? 0, "Indica inconsistência temporal no Salesforce.");

    var factors = new[]
    {
        new { name = "Densidade", points = snapshot.DensityPoints, maximum = configuration.Weights.Density, value = snapshot.DensityVsAverage, formula = $"{snapshot.TotalCases} chamados ÷ ({snapshot.ActiveStores} lojas × {snapshot.BusinessDays} dias úteis) ÷ média {snapshot.AverageDensity:N6}" },
        new { name = "Crescimento", points = snapshot.GrowthPoints, maximum = configuration.Weights.Growth, value = snapshot.RecentGrowthRate, formula = $"{snapshot.TotalCases} ÷ média mensal histórica {(historicalTotal / 3m):N2} − 1" },
        new { name = "SLA", points = snapshot.SlaPoints, maximum = configuration.Weights.Sla, value = snapshot.SlaViolatedRate, formula = $"{caseQuality?.sla ?? 0} violados ÷ {snapshot.TotalCases} chamados" },
        new { name = "FCR", points = snapshot.FcrPoints, maximum = configuration.Weights.Fcr, value = snapshot.FcrRate, formula = $"{caseQuality?.fcr ?? 0} com FCR ÷ {snapshot.TotalCases} chamados" },
        new { name = "Criticidade", points = snapshot.CriticalPoints, maximum = configuration.Weights.Criticality, value = snapshot.CriticalRate, formula = $"{caseQuality?.critical ?? 0} críticos ÷ {snapshot.TotalCases} chamados" },
        new { name = "Issue/JIRA", points = snapshot.IssuePoints, maximum = configuration.Weights.Issue, value = snapshot.IssueRate, formula = $"{caseQuality?.jira ?? 0} com Issue ÷ {snapshot.TotalCases} chamados" },
        new { name = "Recorrência", points = snapshot.RecurrencePoints, maximum = configuration.Weights.Recurrence, value = snapshot.RecurrenceRate, formula = $"Chamados recorrentes na janela de {configuration.RecurrenceWindowDays} dias ÷ {snapshot.TotalCases}" }
    };
    return Results.Ok(new
    {
        group = new { snapshot.Id, snapshot.EconomicGroup, displayName = DisplayGroup(snapshot.EconomicGroup), groupKey = ExtractGroupKey(snapshot.EconomicGroup), snapshot.PeriodStart, snapshot.PeriodEndExclusive, snapshot.Score, snapshot.RiskBand, snapshot.MainReason, snapshot.CalculatedAt },
        calculation = new { snapshot.ActiveStores, snapshot.BusinessDays, snapshot.TotalCases, snapshot.Density, snapshot.AverageDensity, snapshot.DensityVsAverage, historicalTotal, score = snapshot.Score, factors, rule = new { version = rule.Id, rule.Name, rule.PublishedAt, rule.CreatedBy, rule.Justification } },
        grouping = new { accounts = accountRows, totalAccounts = accounts.Count, activeStores = accountRows.Count(x => x.activeStore), parentIds, cnpjRoots = accounts.Select(x => x.CnpjRoot).Where(x => x != null).Distinct().ToArray(), reportedGroups },
        quality = new { cases = caseQuality, anomalies }
    });
}).RequireAuthorization("Viewer");

app.MapGet("/api/v1/audit/groups/{id:long}/cases", async (
    long id, int? page, int? pageSize, bool? anomaliesOnly, string? search, HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    var snapshot = await db.GroupScoreSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (snapshot is null) return Results.NotFound();
    var rule = await db.ScoreRuleVersions.AsNoTracking().SingleAsync(x => x.Id == snapshot.ScoreRuleVersionId, cancellationToken);
    var configuration = InitialScoreRules.Parse(rule.ConfigurationJson);
    var start = DateTime.SpecifyKind(snapshot.PeriodStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    var end = DateTime.SpecifyKind(snapshot.PeriodEndExclusive.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    var historyStart = start.AddDays(-60);
    var recurrenceRows = await db.Cases.AsNoTracking()
        .Where(x => x.EconomicGroup == snapshot.EconomicGroup && x.SalesforceCreatedAt >= start.AddDays(-configuration.RecurrenceWindowDays) && x.SalesforceCreatedAt < end)
        .Where(x => (x.TaxonomyLevel4 ?? x.TaxonomyLevel3 ?? x.TaxonomyLevel2 ?? x.TaxonomyDescription) != null && (x.TaxonomyLevel4 ?? x.TaxonomyLevel3 ?? x.TaxonomyLevel2 ?? x.TaxonomyDescription) != "")
        .OrderBy(x => x.TaxonomyLevel4 ?? x.TaxonomyLevel3 ?? x.TaxonomyLevel2 ?? x.TaxonomyDescription).ThenBy(x => x.SalesforceCreatedAt)
        .Select(x => new { x.SalesforceId, x.SalesforceCreatedAt, Taxonomy = x.TaxonomyLevel4 ?? x.TaxonomyLevel3 ?? x.TaxonomyLevel2 ?? x.TaxonomyDescription }).ToListAsync(cancellationToken);
    var recurringIds = new HashSet<string>(); string? previousTheme = null; DateTime previousDate = default;
    foreach (var row in recurrenceRows)
    {
        if (row.SalesforceCreatedAt >= start && row.Taxonomy == previousTheme && row.SalesforceCreatedAt - previousDate <= TimeSpan.FromDays(configuration.RecurrenceWindowDays)) recurringIds.Add(row.SalesforceId);
        previousTheme = row.Taxonomy; previousDate = row.SalesforceCreatedAt;
    }
    var query = db.Cases.AsNoTracking().Where(x => x.EconomicGroup == snapshot.EconomicGroup && x.SalesforceCreatedAt >= historyStart && x.SalesforceCreatedAt < end);
    if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.CaseNumber.Contains(search.Trim()) || x.SalesforceId.Contains(search.Trim()));
    if (anomaliesOnly == true) query = query.Where(x => x.SlaViolated == null || x.FirstContactResolution == null || x.AccountSalesforceId == null || (x.TaxonomyLevel4 ?? x.TaxonomyLevel3 ?? x.TaxonomyLevel2 ?? x.TaxonomyDescription) == null || (x.ClosedAt != null && x.ClosedAt < x.SalesforceCreatedAt));
    var safePage = Math.Max(page ?? 1, 1); var safePageSize = Math.Clamp(pageSize ?? 50, 1, 200); var total = await query.CountAsync(cancellationToken);
    var rawItems = await query.OrderByDescending(x => x.SalesforceCreatedAt).Skip((safePage - 1) * safePageSize).Take(safePageSize)
        .Select(x => new { x.SalesforceId, x.CaseNumber, x.AccountSalesforceId, x.SalesforceCreatedAt, x.ClosedAt, x.Status, x.Priority, x.SlaViolated, x.FirstContactResolution, x.JiraIssueCode, x.JiraIssueType, x.Product, x.OpeningVertical, x.Brand, taxonomy = x.TaxonomyLevel4 ?? x.TaxonomyLevel3 ?? x.TaxonomyLevel2 ?? x.TaxonomyDescription }).ToListAsync(cancellationToken);
    var items = rawItems.Select(item =>
    {
        var current = item.SalesforceCreatedAt >= start;
        object? Signal(bool active, int points) => active ? new { points } : null;
        return new
        {
            item.SalesforceId, item.CaseNumber, item.AccountSalesforceId, item.SalesforceCreatedAt, item.ClosedAt,
            item.Status, item.Priority, item.SlaViolated, item.FirstContactResolution, item.JiraIssueCode, item.JiraIssueType,
            item.Product, item.OpeningVertical, item.Brand, item.taxonomy,
            evidence = new
            {
                density = Signal(current, snapshot.DensityPoints),
                growth = Signal(true, snapshot.GrowthPoints),
                sla = Signal(current && item.SlaViolated == true, snapshot.SlaPoints),
                fcr = Signal(current && item.FirstContactResolution != true, snapshot.FcrPoints),
                criticality = Signal(current && item.Priority is not null && configuration.CriticalPriorities.Contains(item.Priority), snapshot.CriticalPoints),
                issue = Signal(current && !string.IsNullOrWhiteSpace(item.JiraIssueCode), snapshot.IssuePoints),
                recurrence = Signal(current && recurringIds.Contains(item.SalesforceId), snapshot.RecurrencePoints)
            }
        };
    }).ToList();
    return Results.Ok(new { page = safePage, pageSize = safePageSize, total, items });
}).RequireAuthorization("Viewer");

app.MapGet("/api/v1/risk-score/groups/{id:long}/evolution", async (long id, HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    var reference = await db.GroupScoreSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (reference is null) return Results.NotFound();
    var items = await db.GroupScoreSnapshots.AsNoTracking()
        .Where(x => x.SnapshotKind == "monthly" && x.ScoreRuleVersionId == reference.ScoreRuleVersionId && x.EconomicGroup == reference.EconomicGroup)
        .OrderBy(x => x.PeriodStart)
        .Select(x => new { x.PeriodStart, x.PeriodEndExclusive, x.TotalCases, x.Density, x.Score, x.RiskBand })
        .ToListAsync(cancellationToken);
    return Results.Ok(new { reference.EconomicGroup, items });
});

app.MapGet("/api/v1/risk-score/groups/{id:long}/accounts", async (long id, HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    var snapshot = await db.GroupScoreSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (snapshot is null) return Results.NotFound();
    var start = DateTime.SpecifyKind(snapshot.PeriodStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    var end = DateTime.SpecifyKind(snapshot.PeriodEndExclusive.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    var caseMetrics = await db.Cases.AsNoTracking()
        .Where(x => x.EconomicGroup == snapshot.EconomicGroup && x.SalesforceCreatedAt >= start && x.SalesforceCreatedAt < end && x.AccountSalesforceId != null)
        .GroupBy(x => x.AccountSalesforceId!)
        .Select(group => new
        {
            AccountId = group.Key, TotalCases = group.Count(),
            SlaRate = (decimal)group.Count(x => x.SlaViolated == true) / group.Count(),
            FcrRate = (decimal)group.Count(x => x.FirstContactResolution == true) / group.Count(),
            IssueRate = (decimal)group.Count(x => x.JiraIssueCode != null && x.JiraIssueCode != "") / group.Count()
        }).OrderByDescending(x => x.TotalCases).Take(100).ToListAsync(cancellationToken);
    var accountIds = caseMetrics.Select(x => x.AccountId).ToArray();
    var accounts = await db.Accounts.AsNoTracking().Where(x => accountIds.Contains(x.SalesforceId))
        .ToDictionaryAsync(x => x.SalesforceId, cancellationToken);
    var items = caseMetrics.Select(metric =>
    {
        accounts.TryGetValue(metric.AccountId, out var account);
        return new { accountId = metric.AccountId, name = account?.Name ?? "Conta não localizada", cnpj = account?.Cnpj, brand = account?.Brand, metric.TotalCases, metric.SlaRate, metric.FcrRate, metric.IssueRate };
    });
    return Results.Ok(new { snapshot.EconomicGroup, items });
});

app.MapGet("/api/v1/risk-score/groups/{id:long}/taxonomy", async (long id, HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    var snapshot = await db.GroupScoreSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (snapshot is null) return Results.NotFound();
    var start = DateTime.SpecifyKind(snapshot.PeriodStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    var end = DateTime.SpecifyKind(snapshot.PeriodEndExclusive.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    var history = await db.Cases.AsNoTracking()
        .Where(x => x.EconomicGroup == snapshot.EconomicGroup && x.SalesforceCreatedAt >= start.AddDays(-30) && x.SalesforceCreatedAt < end)
        .Select(x => new
        {
            x.SalesforceCreatedAt, x.SlaViolated, x.JiraIssueCode,
            Theme = x.TaxonomyLevel4 ?? x.TaxonomyLevel3 ?? x.TaxonomyLevel2 ?? x.TaxonomyDescription
        }).Where(x => x.Theme != null && x.Theme != "").OrderBy(x => x.Theme).ThenBy(x => x.SalesforceCreatedAt).ToListAsync(cancellationToken);
    var metrics = new Dictionary<string, (int Total, int Recurrence, int Sla, int Issue)>();
    string? previousTheme = null;
    DateTime previousDate = default;
    foreach (var row in history)
    {
        if (row.SalesforceCreatedAt >= start)
        {
            var current = metrics.GetValueOrDefault(row.Theme!);
            current.Total++;
            if (row.SlaViolated == true) current.Sla++;
            if (!string.IsNullOrWhiteSpace(row.JiraIssueCode)) current.Issue++;
            if (row.Theme == previousTheme && row.SalesforceCreatedAt - previousDate <= TimeSpan.FromDays(30)) current.Recurrence++;
            metrics[row.Theme!] = current;
        }
        previousTheme = row.Theme;
        previousDate = row.SalesforceCreatedAt;
    }
    var items = metrics.OrderByDescending(x => x.Value.Total).Take(30).Select(x => new
    {
        taxonomy = x.Key, totalCases = x.Value.Total, recurrenceCases = x.Value.Recurrence,
        recurrenceRate = (decimal)x.Value.Recurrence / x.Value.Total,
        slaRate = (decimal)x.Value.Sla / x.Value.Total, issueRate = (decimal)x.Value.Issue / x.Value.Total
    });
    return Results.Ok(new { snapshot.EconomicGroup, items });
});

app.MapGet("/api/v1/risk-score/groups/{id:long}/action-plan", async (long id, HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    var snapshot = await db.GroupScoreSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (snapshot is null) return Results.NotFound();
    var plan = await db.ActionPlans.AsNoTracking().SingleOrDefaultAsync(x => x.EconomicGroup == snapshot.EconomicGroup, cancellationToken);
    var history = plan is null
        ? []
        : await db.ActionPlanEvents.AsNoTracking().Where(x => x.ActionPlanId == plan.Id)
            .OrderByDescending(x => x.CreatedAt).Take(50)
            .Select(x => new { x.Id, x.EventType, x.ChangedBy, x.PayloadJson, x.CreatedAt }).ToListAsync(cancellationToken);
    return Results.Ok(new
    {
        snapshot.EconomicGroup, suggestedAction = SuggestedAction(snapshot),
        plan = plan is null ? null : new { plan.Id, plan.Status, plan.Responsible, plan.Notes, plan.CreatedAt, plan.UpdatedAt },
        history
    });
});

app.MapPut("/api/v1/risk-score/groups/{id:long}/action-plan", async (
    long id, SaveActionPlanRequest request, ClaimsPrincipal user, HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    var allowedStatuses = new[] { "not_started", "in_progress", "blocked", "completed" };
    if (!allowedStatuses.Contains(request.Status)) return Results.BadRequest(new { error = "Status inválido." });
    if (request.Notes?.Length > 4000) return Results.BadRequest(new { error = "Observações excedem 4.000 caracteres." });
    var snapshot = await db.GroupScoreSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (snapshot is null) return Results.NotFound();
    var now = DateTime.UtcNow;
    var plan = await db.ActionPlans.SingleOrDefaultAsync(x => x.EconomicGroup == snapshot.EconomicGroup, cancellationToken);
    var eventType = plan is null ? "created" : "updated";
    if (plan is null)
    {
        plan = new HealthScore.Domain.ActionPlan
        {
            EconomicGroup = snapshot.EconomicGroup, Status = request.Status,
            Responsible = CleanText(request.Responsible), Notes = CleanText(request.Notes),
            CreatedAt = now, UpdatedAt = now
        };
        db.ActionPlans.Add(plan);
    }
    else
    {
        plan.Status = request.Status;
        plan.Responsible = CleanText(request.Responsible);
        plan.Notes = CleanText(request.Notes);
        plan.UpdatedAt = now;
    }
    await db.SaveChangesAsync(cancellationToken);
    db.ActionPlanEvents.Add(new HealthScore.Domain.ActionPlanEvent
    {
        ActionPlanId = plan.Id, EventType = eventType,
        ChangedBy = user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown-user",
        PayloadJson = JsonSerializer.Serialize(new { plan.Status, plan.Responsible, plan.Notes }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        CreatedAt = now
    });
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { plan.Id, plan.Status, plan.Responsible, plan.Notes, plan.CreatedAt, plan.UpdatedAt });
}).RequireAuthorization("Operator");

app.MapGet("/api/v1/score-config", async (HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    var versions = await db.ScoreRuleVersions.AsNoTracking().OrderByDescending(x => x.Id).ToListAsync(cancellationToken);
    return Results.Ok(versions.Select(x => new
    {
        version = x.Id, x.Name, x.Status, configuration = InitialScoreRules.Parse(x.ConfigurationJson),
        x.CreatedBy, x.Justification, x.CreatedAt, x.PublishedAt
    }));
}).RequireAuthorization("ScoreAdmin");

app.MapPost("/api/v1/score-config/simulate", async (ScoreConfiguration proposed, HealthScoreDbContext db, CancellationToken cancellationToken) =>
{
    var validation = ValidateConfiguration(proposed);
    if (validation is not null) return Results.BadRequest(new { error = validation });
    var active = await db.ScoreRuleVersions.AsNoTracking().Where(x => x.Status == "published").OrderByDescending(x => x.Id).FirstAsync(cancellationToken);
    var current = InitialScoreRules.Parse(active.ConfigurationJson);
    var period = await db.GroupScoreSnapshots.AsNoTracking()
        .Where(x => x.SnapshotKind == "rolling30" && x.ScoreRuleVersionId == active.Id)
        .MaxAsync(x => (DateOnly?)x.PeriodEndExclusive, cancellationToken);
    if (!period.HasValue) return Results.Conflict(new { error = "Ainda não há snapshots para simular." });
    var snapshots = await db.GroupScoreSnapshots.AsNoTracking()
        .Where(x => x.SnapshotKind == "rolling30" && x.ScoreRuleVersionId == active.Id && x.PeriodEndExclusive == period.Value)
        .ToListAsync(cancellationToken);
    var simulated = snapshots.Select(x => new { Current = x, Score = RecalculateScore(x, current.Weights, proposed.Weights) })
        .Select(x => new { x.Current.EconomicGroup, currentScore = x.Current.Score, simulatedScore = x.Score, currentBand = x.Current.RiskBand, simulatedBand = InitialScoreRules.RiskBand(x.Score, proposed.Bands) })
        .ToList();
    return Results.Ok(new
    {
        groups = simulated.Count,
        currentAverage = simulated.Average(x => (decimal)x.currentScore),
        simulatedAverage = simulated.Average(x => (decimal)x.simulatedScore),
        changedBands = simulated.Count(x => x.currentBand != x.simulatedBand),
        distribution = simulated.GroupBy(x => x.simulatedBand).ToDictionary(x => x.Key, x => x.Count()),
        largestChanges = simulated.OrderByDescending(x => Math.Abs(x.simulatedScore - x.currentScore)).Take(10)
    });
}).RequireAuthorization("ScoreAdmin");

app.MapPost("/api/v1/score-config/publish", async (
    PublishScoreConfigurationRequest request, ClaimsPrincipal user, HealthScoreDbContext db, IAnalyticsService analytics, CancellationToken cancellationToken) =>
{
    var validation = ValidateConfiguration(request.Configuration);
    if (validation is not null) return Results.BadRequest(new { error = validation });
    if (string.IsNullOrWhiteSpace(request.Justification)) return Results.BadRequest(new { error = "A justificativa é obrigatória." });
    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    await db.ScoreRuleVersions.Where(x => x.Status == "published")
        .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "archived"), cancellationToken);
    var now = DateTime.UtcNow;
    var version = new HealthScore.Domain.ScoreRuleVersion
    {
        Name = string.IsNullOrWhiteSpace(request.Name) ? $"Regra publicada em {now:yyyy-MM-dd}" : request.Name.Trim(),
        Status = "published", ConfigurationJson = InitialScoreRules.AsJson(request.Configuration),
        CreatedBy = user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown-user",
        Justification = request.Justification.Trim(), CreatedAt = now, PublishedAt = now
    };
    db.ScoreRuleVersions.Add(version);
    await db.SaveChangesAsync(cancellationToken);
    var result = await analytics.RebuildAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return Results.Ok(new { version = version.Id, analytics = result });
}).RequireAuthorization("ScoreAdmin");

app.Run();

static string SuggestedAction(HealthScore.Domain.GroupScoreSnapshot snapshot)
{
    if (snapshot.Score >= 70 && snapshot.IssueRate >= .15m) return "Acionar Produto/P&D para plano de correção e comunicação executiva com o cliente.";
    if (snapshot.Score >= 70 && snapshot.SlaViolatedRate >= .20m) return "Executar plano de recuperação de SLA e acompanhamento semanal.";
    if (snapshot.DensityVsAverage >= 2m) return "Analisar os principais ofensores e propor um plano preventivo com o cliente.";
    if (snapshot.RecurrenceRate >= .20m) return "Conduzir análise de causa raiz por tema e revisar a base de conhecimento.";
    if (snapshot.CriticalRate >= .20m) return "Avaliar incidentes operacionais e priorizar temas de maior impacto.";
    if (snapshot.Score >= 50) return "Monitorar semanalmente e abrir plano de ação com responsáveis definidos.";
    return "Manter monitoramento; sem ação imediata recomendada.";
}

static string? ValidateConfiguration(ScoreConfiguration configuration)
{
    if (configuration.Weights.Total != 100) return "A soma dos pesos deve ser exatamente 100.";
    if (new[] { configuration.Weights.Density, configuration.Weights.Growth, configuration.Weights.Sla, configuration.Weights.Fcr, configuration.Weights.Criticality, configuration.Weights.Issue, configuration.Weights.Recurrence }.Any(x => x < 0)) return "Pesos não podem ser negativos.";
    if (configuration.Bands.LowMax < 0 || configuration.Bands.LowMax >= configuration.Bands.AttentionMax || configuration.Bands.AttentionMax >= configuration.Bands.HighMax || configuration.Bands.HighMax >= 100) return "As faixas devem ser crescentes e menores que 100.";
    if (configuration.RecurrenceWindowDays is < 1 or > 365) return "A janela de recorrência deve ficar entre 1 e 365 dias.";
    return null;
}

static int RecalculateScore(HealthScore.Domain.GroupScoreSnapshot snapshot, ScoreWeights current, ScoreWeights proposed)
{
    static int Scale(int points, int oldMaximum, int newMaximum) => oldMaximum == 0 ? 0 : (int)Math.Round((decimal)points / oldMaximum * newMaximum, MidpointRounding.AwayFromZero);
    return Math.Min(100,
        Scale(snapshot.DensityPoints, current.Density, proposed.Density) +
        Scale(snapshot.GrowthPoints, current.Growth, proposed.Growth) +
        Scale(snapshot.SlaPoints, current.Sla, proposed.Sla) +
        Scale(snapshot.FcrPoints, current.Fcr, proposed.Fcr) +
        Scale(snapshot.CriticalPoints, current.Criticality, proposed.Criticality) +
        Scale(snapshot.IssuePoints, current.Issue, proposed.Issue) +
        Scale(snapshot.RecurrencePoints, current.Recurrence, proposed.Recurrence));
}

static string? CleanText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
static string NormalizeSnapshotKind(string? value) => value == "monthly" ? "monthly" : "rolling30";
static string Csv(object? value)
{
    var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    if (text.Length > 0 && "=+-@".Contains(text[0])) text = "'" + text;
    return '"' + text.Replace("\"", "\"\"") + '"';
}
static string DisplayGroup(string value) => System.Text.RegularExpressions.Regex.Replace(value, @" \[(?:P|C|A):[^\]]+\]$", string.Empty);
static string? ExtractGroupKey(string value)
{
    var match = System.Text.RegularExpressions.Regex.Match(value, @" \[((?:P|C|A):[^\]]+)\]$");
    return match.Success ? match.Groups[1].Value : null;
}
static void AddAnomaly(List<object> target, string severity, string title, int count, string explanation)
{
    if (count > 0) target.Add(new { severity, title, count, explanation });
}

static async Task<(DateOnly Start, DateOnly End, string Kind, HealthScore.Domain.ScoreRuleVersion Rule, TimeZoneInfo? TimeZone, bool Dynamic)?> ResolvePeriodAsync(
    HealthScoreDbContext db, string? range, string? snapshotKind, DateOnly? periodStart, CancellationToken cancellationToken)
{
    var rule = await db.ScoreRuleVersions.AsNoTracking().Where(x => x.Status == "published").OrderByDescending(x => x.Id).FirstAsync(cancellationToken);
    if (range is "today" or "yesterday" or "last7" or "last15")
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var start = range switch { "today" => today, "yesterday" => today.AddDays(-1), "last7" => today.AddDays(-6), _ => today.AddDays(-14) };
        var end = range == "yesterday" ? today : today.AddDays(1);
        return (start, end, range, rule, timeZone, true);
    }
    var kind = NormalizeSnapshotKind(snapshotKind);
    var query = db.GroupScoreSnapshots.AsNoTracking().Where(x => x.SnapshotKind == kind && x.ScoreRuleVersionId == rule.Id);
    if (periodStart.HasValue) query = query.Where(x => x.PeriodStart == periodStart.Value);
    var selected = await query.OrderByDescending(x => x.PeriodEndExclusive).Select(x => new { x.PeriodStart, x.PeriodEndExclusive }).FirstOrDefaultAsync(cancellationToken);
    return selected is null ? null : (selected.PeriodStart, selected.PeriodEndExclusive, kind, rule, null, false);
}

sealed record PublishScoreConfigurationRequest(string Name, string CreatedBy, string Justification, ScoreConfiguration Configuration);
sealed record SaveActionPlanRequest(string Status, string? Responsible, string? Notes, string? ChangedBy);
