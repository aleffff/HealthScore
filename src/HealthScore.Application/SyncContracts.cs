using System.Text.Json;

namespace HealthScore.Application;

public interface ISalesforceClient
{
    IAsyncEnumerable<JsonElement> QueryAsync(string soql, CancellationToken cancellationToken);
}

public interface IFarmaSyncService
{
    Task<SyncSummary> SynchronizeAsync(CancellationToken cancellationToken);
}

public sealed record SyncSummary(int AccountsRead, int AccountsWritten, int CasesRead, int CasesWritten);

public interface IAnalyticsService
{
    Task<AnalyticsSummary> RebuildAsync(CancellationToken cancellationToken);
}

public sealed record AnalyticsSummary(int Groups, int Snapshots, DateOnly PeriodStart, DateOnly PeriodEndExclusive);
