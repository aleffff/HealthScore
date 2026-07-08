using HealthScore.Infrastructure;

namespace HealthScore.UnitTests;

public sealed class AccountGroupResolverTests
{
    [Theory]
    [InlineData("04.252.011/0001-10", "04252011")]
    [InlineData("04.252.011/0002-09", "04252011")]
    [InlineData("11.111.111/1111-11", null)]
    [InlineData(null, null)]
    public void Cnpj_root_is_returned_only_for_valid_cnpj(string? cnpj, string? expected) =>
        Assert.Equal(expected, AccountGroupResolver.CnpjRoot(cnpj));

    [Fact]
    public void Shared_parent_and_cnpj_root_are_combined_transitively()
    {
        var accounts = new[]
        {
            Account("A", "Loja A", "04.252.011/0001-10", "P1", "Grupo Pai"),
            Account("B", "Loja B", "04.252.011/0002-09", "P1", "Grupo Pai"),
            Account("C", "Loja C", "04.252.011/0003-81", null, null),
        };

        var result = AccountGroupResolver.Resolve(accounts);

        Assert.Single(result.Select(x => x.Key).Distinct());
        Assert.All(result, item => Assert.Equal("Grupo Pai [P:P1]", item.Name));
    }

    [Fact]
    public void Blank_parent_and_invalid_cnpj_do_not_merge_unrelated_accounts()
    {
        var result = AccountGroupResolver.Resolve([
            Account("A", "Loja A", "11.111.111/1111-11", null, null),
            Account("B", "Loja B", null, null, null),
        ]);

        Assert.Equal(2, result.Select(x => x.Key).Distinct().Count());
    }

    [Fact]
    public void Equal_labels_from_different_components_are_disambiguated()
    {
        var result = AccountGroupResolver.Resolve([
            new("A", "Loja A", null, "P1", "Mesmo grupo", null),
            new("B", "Loja B", null, "P2", "Mesmo grupo", null),
        ]);

        Assert.Equal(2, result.Select(x => x.Name).Distinct().Count());
        Assert.All(result, item => Assert.Contains(item.Key, item.Name));
    }

    private static AccountGroupCandidate Account(string id, string name, string? cnpj, string? parentId, string? parentName) =>
        new(id, name, cnpj, parentId, parentName, null);
}
