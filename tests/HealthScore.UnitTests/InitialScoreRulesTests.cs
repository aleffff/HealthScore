using HealthScore.Infrastructure;

namespace HealthScore.UnitTests;

public sealed class InitialScoreRulesTests
{
    [Theory]
    [InlineData(1.00, 0)]
    [InlineData(1.01, 5)]
    [InlineData(2.00, 10)]
    [InlineData(3.01, 25)]
    public void Density_thresholds_are_deterministic(decimal value, int expected) =>
        Assert.Equal(expected, InitialScoreRules.DensityPoints(value));

    [Theory]
    [InlineData(0.80, 0)]
    [InlineData(0.70, 3)]
    [InlineData(0.50, 7)]
    [InlineData(0.40, 10)]
    public void Fcr_is_an_inverse_risk_factor(decimal value, int expected) =>
        Assert.Equal(expected, InitialScoreRules.FcrPoints(value));

    [Fact]
    public void Main_reason_uses_documented_severity_tie_breaker()
    {
        var points = new Dictionary<string, int>
        {
            ["Densidade"] = 10, ["Crescimento"] = 0, ["SLA"] = 10, ["FCR"] = 0,
            ["Criticidade"] = 10, ["Issue/JIRA"] = 10, ["Recorrência"] = 10
        };

        Assert.Equal("Issue/JIRA", InitialScoreRules.MainReason(points));
    }

    [Fact]
    public void Configuration_round_trip_preserves_a_weight_total_of_100()
    {
        var json = InitialScoreRules.AsJson();
        var parsed = InitialScoreRules.Parse(json);

        Assert.Equal(100, parsed.Weights.Total);
        Assert.DoesNotContain("\"total\"", json, StringComparison.OrdinalIgnoreCase);
    }
}
