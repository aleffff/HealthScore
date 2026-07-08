using HealthScore.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HealthScore.Infrastructure;

public sealed record AccountGroupCandidate(
    string SalesforceId, string Name, string? Cnpj, string? ParentSalesforceId,
    string? ParentName, string? ReportedEconomicGroup);

public sealed record ResolvedAccountGroup(string SalesforceId, string Key, string Name, string? CnpjRoot);

public sealed class AccountGroupResolver(HealthScoreDbContext db, ILogger<AccountGroupResolver> logger)
{
    public async Task ResolveAsync(CancellationToken cancellationToken)
    {
        var accounts = await db.Accounts.ToListAsync(cancellationToken);
        var resolved = Resolve(accounts.Select(x => new AccountGroupCandidate(
            x.SalesforceId, x.Name, x.Cnpj, x.ParentSalesforceId, x.ParentName, x.ReportedEconomicGroup)));
        var byId = resolved.ToDictionary(x => x.SalesforceId);
        foreach (var account in accounts)
        {
            var group = byId[account.SalesforceId];
            account.CnpjRoot = group.CnpjRoot;
            account.EconomicGroup = group.Name;
        }
        await db.SaveChangesAsync(cancellationToken);

        var affectedCases = await db.Database.ExecuteSqlRawAsync("""
            UPDATE cases AS c
            INNER JOIN accounts AS a ON a.SalesforceId = c.AccountSalesforceId
            SET c.EconomicGroup = a.EconomicGroup
            WHERE NOT (c.EconomicGroup <=> a.EconomicGroup)
            """, cancellationToken);
        db.ChangeTracker.Clear();
        logger.LogInformation("Account groups resolved: {Accounts} accounts, {Groups} groups, {Cases} cases reassigned",
            resolved.Count, resolved.Select(x => x.Key).Distinct().Count(), affectedCases);
    }

    public static IReadOnlyList<ResolvedAccountGroup> Resolve(IEnumerable<AccountGroupCandidate> source)
    {
        var accounts = source.OrderBy(x => x.SalesforceId, StringComparer.Ordinal).ToList();
        var union = new DisjointSet(accounts.Count);
        var indexes = accounts.Select((account, index) => (account.SalesforceId, index)).ToDictionary(x => x.SalesforceId, x => x.index, StringComparer.OrdinalIgnoreCase);

        UnionShared(accounts, union, account => Clean(account.ParentSalesforceId));
        UnionShared(accounts, union, account => CnpjRoot(account.Cnpj));
        for (var index = 0; index < accounts.Count; index++)
            if (Clean(accounts[index].ParentSalesforceId) is { } parentId && indexes.TryGetValue(parentId, out var parentIndex)) union.Union(index, parentIndex);

        var components = accounts.Select((account, index) => (account, index)).GroupBy(x => union.Find(x.index)).ToList();
        var result = new List<ResolvedAccountGroup>(accounts.Count);
        foreach (var component in components)
        {
            var members = component.Select(x => x.account).ToList();
            var parentIds = members.Select(x => Clean(x.ParentSalesforceId)).Where(x => x is not null).Cast<string>().OrderBy(x => x, StringComparer.Ordinal).ToList();
            var roots = members.Select(x => CnpjRoot(x.Cnpj)).Where(x => x is not null).Cast<string>().OrderBy(x => x, StringComparer.Ordinal).ToList();
            var key = parentIds.Count > 0 ? $"P:{parentIds[0]}" : roots.Count > 0 ? $"C:{roots[0]}" : $"A:{members[0].SalesforceId}";
            var name = MostFrequent(members.Select(x => Clean(x.ParentName)))
                ?? MostFrequent(members.Select(x => Clean(x.ReportedEconomicGroup)))
                ?? (members.Count > 1 && roots.Count > 0 ? $"Grupo CNPJ {roots[0]}" : members[0].Name);
            foreach (var member in members)
                result.Add(new ResolvedAccountGroup(member.SalesforceId, key, name, CnpjRoot(member.Cnpj)));
        }

        // The suffix is the stable technical identity. It prevents label/collation collisions in snapshots and action plans.
        return result.Select(x => x with { Name = UniqueName(x.Name, x.Key) }).ToList();
    }

    public static string? CnpjRoot(string? value)
    {
        var digits = value is null ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
        return IsValidCnpj(digits) ? digits[..8] : null;
    }

    public static bool IsValidCnpj(string value)
    {
        if (value.Length != 14 || value.Distinct().Count() == 1 || value.Any(x => !char.IsDigit(x))) return false;
        static int Digit(string digits, int[] weights)
        {
            var sum = weights.Select((weight, index) => weight * (digits[index] - '0')).Sum();
            var remainder = sum % 11;
            return remainder < 2 ? 0 : 11 - remainder;
        }
        var first = Digit(value, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        var second = Digit(value, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return value[12] - '0' == first && value[13] - '0' == second;
    }

    private static void UnionShared(IReadOnlyList<AccountGroupCandidate> accounts, DisjointSet union, Func<AccountGroupCandidate, string?> selector)
    {
        var firstByValue = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < accounts.Count; index++)
        {
            var value = selector(accounts[index]);
            if (value is null) continue;
            if (firstByValue.TryGetValue(value, out var first)) union.Union(first, index); else firstByValue[value] = index;
        }
    }

    private static string? MostFrequent(IEnumerable<string?> values) => values.Where(x => x is not null).Cast<string>()
        .GroupBy(x => x, StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => x.Key).FirstOrDefault();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string UniqueName(string name, string key)
    {
        var suffix = $" [{key}]";
        var prefix = name.Length + suffix.Length <= 255 ? name : name[..(255 - suffix.Length)];
        return prefix + suffix;
    }

    private sealed class DisjointSet(int count)
    {
        private readonly int[] parent = Enumerable.Range(0, count).ToArray();
        private readonly byte[] rank = new byte[count];
        public int Find(int value) { if (parent[value] != value) parent[value] = Find(parent[value]); return parent[value]; }
        public void Union(int left, int right) { left = Find(left); right = Find(right); if (left == right) return; if (rank[left] < rank[right]) parent[left] = right; else { parent[right] = left; if (rank[left] == rank[right]) rank[left]++; } }
    }
}
