using HealthScore.Infrastructure;

namespace HealthScore.UnitTests;

public sealed class AnalyticsFilterTests
{
    [Fact]
    public void Filter_normalizes_values_and_rejects_unknown_issue_mode()
    {
        var filter = new AnalyticsFilter(" Farma ", " ", "Suporte", "invalid");

        Assert.Equal("Farma", filter.Brand);
        Assert.Null(filter.Product);
        Assert.Equal("Suporte", filter.Scope);
        Assert.Null(filter.Issue);
    }

    [Theory]
    [InlineData("with")]
    [InlineData("without")]
    public void Filter_accepts_supported_issue_modes(string mode)
    {
        Assert.Equal(mode, new AnalyticsFilter(null, null, null, mode).Issue);
    }
}
