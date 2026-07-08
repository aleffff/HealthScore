using HealthScore.Application;
using HealthScore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HealthScore.Worker;

public sealed class Worker(
    IServiceScopeFactory scopeFactory,
    IOptions<SyncOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(Math.Max(options.Value.IntervalMinutes, 1));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await MigrateAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IFarmaSyncService>();
                var result = await service.SynchronizeAsync(stoppingToken);
                var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
                var analyticsResult = await analytics.RebuildAsync(stoppingToken);
                logger.LogInformation(
                    "FARMA cycle completed. Accounts {AccountsRead}/{AccountsWritten}; Cases {CasesRead}/{CasesWritten}; Groups {Groups}; Snapshots {Snapshots}",
                    result.AccountsRead, result.AccountsWritten, result.CasesRead, result.CasesWritten,
                    analyticsResult.Groups, analyticsResult.Snapshots);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "FARMA sync failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HealthScoreDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await db.SyncRuns
            .Where(x => x.Status == "running")
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.Status, "interrupted")
                .SetProperty(x => x.FinishedAt, DateTime.UtcNow)
                .SetProperty(x => x.Error, "Worker stopped before the synchronization completed."),
                cancellationToken);
    }
}
