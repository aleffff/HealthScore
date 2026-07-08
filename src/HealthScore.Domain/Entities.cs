namespace HealthScore.Domain;

public sealed class AccountRecord
{
    public long Id { get; set; }
    public required string SalesforceId { get; set; }
    public required string Name { get; set; }
    public string? Cnpj { get; set; }
    public string? CnpjRoot { get; set; }
    public string? ParentSalesforceId { get; set; }
    public string? ParentName { get; set; }
    public string? ReportedEconomicGroup { get; set; }
    public string? EconomicGroup { get; set; }
    public string? Brand { get; set; }
    public required string Vertical { get; set; }
    public string? Status { get; set; }
    public DateTime SalesforceCreatedAt { get; set; }
    public DateTime SalesforceModifiedAt { get; set; }
    public DateTime SyncedAt { get; set; }
}

public sealed class CaseRecord
{
    public long Id { get; set; }
    public required string SalesforceId { get; set; }
    public required string CaseNumber { get; set; }
    public string? AccountSalesforceId { get; set; }
    public string? ReportedEconomicGroup { get; set; }
    public string? EconomicGroup { get; set; }
    public string? Brand { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public bool? SlaViolated { get; set; }
    public bool? FirstContactResolution { get; set; }
    public string? JiraIssueCode { get; set; }
    public string? JiraIssueType { get; set; }
    public string? Product { get; set; }
    public string? OpeningVertical { get; set; }
    public string? TaxonomyLevel1 { get; set; }
    public string? TaxonomyLevel2 { get; set; }
    public string? TaxonomyLevel3 { get; set; }
    public string? TaxonomyLevel4 { get; set; }
    public string? TaxonomyDescription { get; set; }
    public DateTime SalesforceCreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime SalesforceModifiedAt { get; set; }
    public DateTime SyncedAt { get; set; }
}

public sealed class SyncWatermark
{
    public required string EntityName { get; set; }
    public DateTime Value { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class SyncRun
{
    public long Id { get; set; }
    public required string EntityName { get; set; }
    public required string Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int RecordsRead { get; set; }
    public int RecordsWritten { get; set; }
    public string? Error { get; set; }
}

public sealed class EconomicGroup
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public int ActiveStores { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class BusinessCalendarDay
{
    public DateOnly Date { get; set; }
    public bool IsBusinessDay { get; set; }
    public string? HolidayName { get; set; }
}

public sealed class ScoreRuleVersion
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Status { get; set; }
    public required string ConfigurationJson { get; set; }
    public required string CreatedBy { get; set; }
    public required string Justification { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime PublishedAt { get; set; }
}

public sealed class GroupScoreSnapshot
{
    public long Id { get; set; }
    public required string EconomicGroup { get; set; }
    public required string SnapshotKind { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEndExclusive { get; set; }
    public int ScoreRuleVersionId { get; set; }
    public int ActiveStores { get; set; }
    public int BusinessDays { get; set; }
    public int TotalCases { get; set; }
    public decimal Density { get; set; }
    public decimal AverageDensity { get; set; }
    public decimal DensityVsAverage { get; set; }
    public decimal SlaViolatedRate { get; set; }
    public decimal FcrRate { get; set; }
    public decimal IssueRate { get; set; }
    public decimal CriticalRate { get; set; }
    public decimal RecurrenceRate { get; set; }
    public decimal RecentGrowthRate { get; set; }
    public int DensityPoints { get; set; }
    public int GrowthPoints { get; set; }
    public int SlaPoints { get; set; }
    public int FcrPoints { get; set; }
    public int CriticalPoints { get; set; }
    public int IssuePoints { get; set; }
    public int RecurrencePoints { get; set; }
    public int Score { get; set; }
    public required string RiskBand { get; set; }
    public required string MainReason { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public sealed class ActionPlan
{
    public long Id { get; set; }
    public required string EconomicGroup { get; set; }
    public required string Status { get; set; }
    public string? Responsible { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ActionPlanEvent
{
    public long Id { get; set; }
    public long ActionPlanId { get; set; }
    public required string EventType { get; set; }
    public required string ChangedBy { get; set; }
    public required string PayloadJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
