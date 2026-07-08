using System.Globalization;
using System.Text.Json;
using HealthScore.Application;
using HealthScore.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthScore.Infrastructure;

public sealed class FarmaSyncService(
    HealthScoreDbContext db,
    ISalesforceClient salesforce,
    IOptions<SyncOptions> options,
    ILogger<FarmaSyncService> logger) : IFarmaSyncService
{
    private const string AccountEntity = "Account";
    private const string CaseEntity = "Case";
    private readonly SyncOptions _options = options.Value;

    public async Task<SyncSummary> SynchronizeAsync(CancellationToken cancellationToken)
    {
        var accounts = await SyncAccountsAsync(cancellationToken);
        var cases = await SyncCasesAsync(cancellationToken);
        return new SyncSummary(accounts.Read, accounts.Written, cases.Read, cases.Written);
    }

    private async Task<(int Read, int Written)> SyncAccountsAsync(CancellationToken cancellationToken)
    {
        var since = await GetWatermarkAsync(AccountEntity, cancellationToken);
        if (since.HasValue)
        {
            since = since.Value.AddMinutes(-_options.WatermarkOverlapMinutes);
        }
        var soql = FarmaSalesforceQueries.Accounts(since);

        return await ExecuteRunAsync(AccountEntity, salesforce.QueryAsync(soql, cancellationToken), MapAccount, cancellationToken);
    }

    private async Task<(int Read, int Written)> SyncCasesAsync(CancellationToken cancellationToken)
    {
        var watermark = await GetWatermarkAsync(CaseEntity, cancellationToken);
        var since = (watermark ?? DateTime.UtcNow.AddDays(-_options.InitialLookbackDays))
            .AddMinutes(-_options.WatermarkOverlapMinutes);
        var soql = FarmaSalesforceQueries.Cases(since);

        return await ExecuteRunAsync(CaseEntity, salesforce.QueryAsync(soql, cancellationToken), MapCase, cancellationToken);
    }

    private async Task<(int Read, int Written)> ExecuteRunAsync<TEntity>(
        string entityName,
        IAsyncEnumerable<JsonElement> source,
        Func<JsonElement, TEntity> map,
        CancellationToken cancellationToken) where TEntity : class
    {
        var run = new SyncRun { EntityName = entityName, Status = "running", StartedAt = DateTime.UtcNow };
        db.SyncRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        var read = 0;
        var written = 0;
        DateTime? maxStamp = null;
        var batch = new List<TEntity>(500);

        try
        {
            await foreach (var record in source.WithCancellation(cancellationToken))
            {
                read++;
                var entity = map(record);
                batch.Add(entity);
                var stamp = GetDateTime(record, "SystemModstamp");
                if (!maxStamp.HasValue || stamp > maxStamp.Value)
                {
                    maxStamp = stamp;
                }

                if (batch.Count == 500)
                {
                    written += await UpsertBatchAsync(batch, cancellationToken);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                written += await UpsertBatchAsync(batch, cancellationToken);
            }

            if (maxStamp.HasValue)
            {
                await SaveWatermarkAsync(entityName, maxStamp.Value, cancellationToken);
            }

            run = await db.SyncRuns.SingleAsync(x => x.Id == run.Id, cancellationToken);
            run.Status = "succeeded";
            run.RecordsRead = read;
            run.RecordsWritten = written;
            run.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Salesforce {Entity} sync completed: {Read} read, {Written} written", entityName, read, written);
            return (read, written);
        }
        catch (Exception exception)
        {
            db.ChangeTracker.Clear();
            run = await db.SyncRuns.SingleAsync(x => x.Id == run.Id, cancellationToken);
            run.Status = "failed";
            run.RecordsRead = read;
            run.RecordsWritten = written;
            run.FinishedAt = DateTime.UtcNow;
            run.Error = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task<int> UpsertBatchAsync<TEntity>(IReadOnlyCollection<TEntity> batch, CancellationToken cancellationToken) where TEntity : class
    {
        if (typeof(TEntity) == typeof(AccountRecord))
        {
            var incoming = batch.Cast<AccountRecord>().ToList();
            var ids = incoming.Select(x => x.SalesforceId).ToList();
            var existing = await db.Accounts.Where(x => ids.Contains(x.SalesforceId)).ToDictionaryAsync(x => x.SalesforceId, cancellationToken);
            foreach (var item in incoming)
            {
                if (existing.TryGetValue(item.SalesforceId, out var current))
                {
                    item.Id = current.Id;
                    db.Entry(current).CurrentValues.SetValues(item);
                }
                else
                {
                    db.Accounts.Add(item);
                }
            }
        }
        else
        {
            var incoming = batch.Cast<CaseRecord>().ToList();
            var ids = incoming.Select(x => x.SalesforceId).ToList();
            var existing = await db.Cases.Where(x => ids.Contains(x.SalesforceId)).ToDictionaryAsync(x => x.SalesforceId, cancellationToken);
            foreach (var item in incoming)
            {
                if (existing.TryGetValue(item.SalesforceId, out var current))
                {
                    item.Id = current.Id;
                    db.Entry(current).CurrentValues.SetValues(item);
                }
                else
                {
                    db.Cases.Add(item);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return batch.Count;
    }

    private async Task<DateTime?> GetWatermarkAsync(string entityName, CancellationToken cancellationToken)
    {
        var watermark = await db.SyncWatermarks.AsNoTracking().SingleOrDefaultAsync(x => x.EntityName == entityName, cancellationToken);
        return watermark?.Value;
    }

    private async Task SaveWatermarkAsync(string entityName, DateTime value, CancellationToken cancellationToken)
    {
        var watermark = await db.SyncWatermarks.SingleOrDefaultAsync(x => x.EntityName == entityName, cancellationToken);
        if (watermark is null)
        {
            db.SyncWatermarks.Add(new SyncWatermark { EntityName = entityName, Value = value, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            watermark.Value = value;
            watermark.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static AccountRecord MapAccount(JsonElement record) => new()
    {
        Id = 0,
        SalesforceId = GetString(record, "Id")!,
        Name = GetString(record, "Name") ?? "",
        Cnpj = NormalizeCnpj(GetString(record, "CpfCnpj__c")),
        ParentSalesforceId = Clean(GetString(record, "ParentId")),
        ParentName = Clean(GetNestedString(record, "Parent", "Name")),
        ReportedEconomicGroup = Clean(GetString(record, "GrupoEconomico__c")),
        EconomicGroup = Clean(GetString(record, "GrupoEconomico__c")),
        Brand = Clean(GetString(record, "Marca__c")),
        Vertical = GetString(record, "Vertical__c") ?? "FARMA",
        Status = Clean(GetString(record, "Status__c")),
        SalesforceCreatedAt = GetDateTime(record, "CreatedDate"),
        SalesforceModifiedAt = GetDateTime(record, "SystemModstamp"),
        SyncedAt = DateTime.UtcNow
    };

    private static CaseRecord MapCase(JsonElement record) => new()
    {
        Id = 0,
        SalesforceId = GetString(record, "Id")!,
        CaseNumber = GetString(record, "CaseNumber") ?? "",
        AccountSalesforceId = GetString(record, "AccountId"),
        ReportedEconomicGroup = Clean(GetString(record, "GrupoEconomico__c")),
        EconomicGroup = Clean(GetString(record, "GrupoEconomico__c")),
        Brand = Clean(GetString(record, "Marca__c")),
        Status = Clean(GetString(record, "Status")),
        Priority = Clean(GetString(record, "Priority")),
        SlaViolated = GetBoolean(record, "SLA_violado__c"),
        FirstContactResolution = GetBoolean(record, "FCR__c"),
        JiraIssueCode = Clean(GetString(record, "Issue_Code_Jira__c")),
        JiraIssueType = Clean(GetString(record, "Issue_type_JIRA__c")),
        Product = Clean(GetString(record, "Produto__c")),
        OpeningVertical = Clean(GetString(record, "Vertical_de_Abertura__c")),
        TaxonomyLevel1 = Clean(GetString(record, "Nivel_1__c")),
        TaxonomyLevel2 = Clean(GetString(record, "Nivel_2__c")),
        TaxonomyLevel3 = Clean(GetString(record, "Nivel_3__c")),
        TaxonomyLevel4 = Clean(GetString(record, "Nivel_4__c")),
        TaxonomyDescription = Clean(GetString(record, "Descricao_Taxonomia__c")),
        SalesforceCreatedAt = GetDateTime(record, "CreatedDate"),
        ClosedAt = GetNullableDateTime(record, "ClosedDate"),
        SalesforceModifiedAt = GetDateTime(record, "SystemModstamp"),
        SyncedAt = DateTime.UtcNow
    };

    private static string? GetString(JsonElement record, string property) => record.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() : null;
    private static string? GetNestedString(JsonElement record, string parent, string property) =>
        record.TryGetProperty(parent, out var nested) && nested.ValueKind == JsonValueKind.Object ? GetString(nested, property) : null;
    private static bool? GetBoolean(JsonElement record, string property) => record.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
    private static DateTime GetDateTime(JsonElement record, string property) => DateTime.Parse(GetString(record, property)!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
    private static DateTime? GetNullableDateTime(JsonElement record, string property) => GetString(record, property) is { } value ? DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal) : null;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeCnpj(string? value) => value is null ? null : new string(value.Where(char.IsDigit).ToArray()) is { Length: > 0 } digits ? digits : null;
}
