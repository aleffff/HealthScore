using System.Text.Json;
using System.Text.Json.Serialization;

namespace HealthScore.Infrastructure;

public static class InitialScoreRules
{
    public const int Version = 1;

    public static ScoreConfiguration Default { get; } = new(
        30, 30,
        new[] { "Altíssima", "Alta", "High", "Disaster", "P0", "P1" },
        new ScoreWeights(25, 15, 15, 10, 15, 10, 10),
        new ScoreBands(29, 49, 69));

    public static string AsJson(ScoreConfiguration? configuration = null) =>
        JsonSerializer.Serialize(configuration ?? Default, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static ScoreConfiguration Parse(string json) =>
        JsonSerializer.Deserialize<ScoreConfiguration>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? Default;

    public static int DensityPoints(decimal value) => value <= 1m ? 0 : value <= 1.5m ? 5 : value <= 2m ? 10 : value <= 3m ? 18 : 25;
    public static int GrowthPoints(decimal value) => value <= .10m ? 0 : value <= .30m ? 5 : value <= .60m ? 10 : 15;
    public static int SlaPoints(decimal value) => value <= .05m ? 0 : value <= .15m ? 5 : value <= .30m ? 10 : 15;
    public static int FcrPoints(decimal value) => value >= .75m ? 0 : value >= .60m ? 3 : value >= .45m ? 7 : 10;
    public static int CriticalPoints(decimal value) => value <= .05m ? 0 : value <= .10m ? 5 : value <= .20m ? 10 : 15;
    public static int IssuePoints(decimal value) => value <= .03m ? 0 : value <= .08m ? 3 : value <= .15m ? 7 : 10;
    public static int RecurrencePoints(decimal value) => value <= .05m ? 0 : value <= .10m ? 5 : 10;
    public static int Scale(int basePoints, int baseMaximum, int configuredWeight) =>
        (int)Math.Round((decimal)basePoints / baseMaximum * configuredWeight, MidpointRounding.AwayFromZero);

    public static string RiskBand(int score, ScoreBands? bands = null)
    {
        bands ??= Default.Bands;
        return score <= bands.LowMax ? "Baixo" : score <= bands.AttentionMax ? "Atenção" : score <= bands.HighMax ? "Alto" : "Crítico";
    }

    public static string MainReason(IReadOnlyDictionary<string, int> points)
    {
        var priority = new[] { "Issue/JIRA", "SLA", "Criticidade", "Recorrência", "Densidade", "Crescimento", "FCR" };
        var maximum = points.Values.Max();
        return priority.First(name => points[name] == maximum);
    }
}

public sealed record ScoreConfiguration(
    int PeriodDays,
    int RecurrenceWindowDays,
    string[] CriticalPriorities,
    ScoreWeights Weights,
    ScoreBands Bands);

public sealed record ScoreWeights(int Density, int Growth, int Sla, int Fcr, int Criticality, int Issue, int Recurrence)
{
    [JsonIgnore]
    public int Total => Density + Growth + Sla + Fcr + Criticality + Issue + Recurrence;
}

public sealed record ScoreBands(int LowMax, int AttentionMax, int HighMax);
