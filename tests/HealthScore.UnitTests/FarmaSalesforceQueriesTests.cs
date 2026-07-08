using HealthScore.Infrastructure;

namespace HealthScore.UnitTests;

public sealed class FarmaSalesforceQueriesTests
{
    [Fact]
    public void Accounts_are_strictly_limited_to_farma()
    {
        var query = FarmaSalesforceQueries.Accounts(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.Contains("Vertical__c = 'FARMA'", query, StringComparison.Ordinal);
        Assert.Contains("SystemModstamp > 2026-07-01T12:00:00Z", query, StringComparison.Ordinal);
    }

    [Fact]
    public void Initial_account_load_has_no_date_cutoff()
    {
        var query = FarmaSalesforceQueries.Accounts(null);

        Assert.Contains("Vertical__c = 'FARMA'", query, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemModstamp >", query, StringComparison.Ordinal);
    }

    [Fact]
    public void Cases_are_limited_by_the_related_account_vertical()
    {
        var query = FarmaSalesforceQueries.Cases(DateTime.UnixEpoch);

        Assert.Contains("Account.Vertical__c = 'FARMA'", query, StringComparison.Ordinal);
        Assert.Contains("SLA_violado__c", query, StringComparison.Ordinal);
        Assert.Contains("FCR__c", query, StringComparison.Ordinal);
        Assert.Contains("Issue_Code_Jira__c", query, StringComparison.Ordinal);
    }
}
