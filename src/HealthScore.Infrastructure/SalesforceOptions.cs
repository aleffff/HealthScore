using System.ComponentModel.DataAnnotations;

namespace HealthScore.Infrastructure;

public sealed class SalesforceOptions
{
    public const string SectionName = "Salesforce";

    [Required, Url]
    public string LoginUrl { get; init; } = "https://login.salesforce.com";

    [Required]
    public string ClientId { get; init; } = string.Empty;

    [Required]
    public string ClientSecret { get; init; } = string.Empty;

    [Required]
    public string ApiVersion { get; init; } = "v63.0";
}

public sealed class SyncOptions
{
    public const string SectionName = "Sync";
    public int IntervalMinutes { get; init; } = 60;
    public int InitialLookbackDays { get; init; } = 730;
    public int WatermarkOverlapMinutes { get; init; } = 5;
}
