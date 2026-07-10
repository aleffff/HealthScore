using System.Globalization;
using System.Text;
using HealthScore.Domain;
using Microsoft.EntityFrameworkCore;

namespace HealthScore.Infrastructure;

public sealed class ProductNormalizer(HealthScoreDbContext db)
{
    private static readonly IReadOnlyDictionary<string, int> SourcePriority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["produto_salesforce"] = 1,
        ["produto_comercial"] = 2,
        ["produto_padronizado"] = 3,
        ["produto_salesforce_normalizado"] = 4,
        ["produto_comercial_normalizado"] = 5,
        ["produto_padronizado_normalizado"] = 6,
        ["unidade_negocio_produto"] = 7,
        ["unidade_negocio_produto_normalizado"] = 8
    };

    public async Task<ProductNormalizationMap> LoadAsync(CancellationToken cancellationToken)
    {
        var mappings = await db.ProductMappings.AsNoTracking().ToListAsync(cancellationToken);
        return ProductNormalizationMap.From(mappings);
    }

    public async Task<string?> NormalizeAsync(string? value, CancellationToken cancellationToken)
    {
        var map = await LoadAsync(cancellationToken);
        return map.Normalize(value);
    }

    internal static int Priority(string sourceSystem) =>
        SourcePriority.TryGetValue(sourceSystem, out var priority) ? priority : 99;
}

public sealed class ProductNormalizationMap
{
    private static readonly IReadOnlyList<ProductMapping> SupplementalMappings =
    [
        new() { Id = -1, Vertical = "FARMA", StandardProduct = "BIG SISTEMAS", SourceSystem = "produto_salesforce", SourceValue = "LINX FARMA BIG", CommercialProduct = "LINX BIG FARMA", UpdatedAt = DateTime.UnixEpoch, UpdatedBy = "code:supplemental-product-aliases" },
        new() { Id = -2, Vertical = "FARMA", StandardProduct = "BIG SISTEMAS", SourceSystem = "produto_salesforce_normalizado", SourceValue = "LINX FARMA BIG", CommercialProduct = "LINX BIG FARMA", UpdatedAt = DateTime.UnixEpoch, UpdatedBy = "code:supplemental-product-aliases" }
    ];

    private readonly IReadOnlyDictionary<string, string> _byExact;
    private readonly IReadOnlyDictionary<string, string> _byNormalized;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _sourceValuesByStandard;

    private ProductNormalizationMap(
        IReadOnlyDictionary<string, string> byExact,
        IReadOnlyDictionary<string, string> byNormalized,
        IReadOnlyDictionary<string, IReadOnlyList<string>> sourceValuesByStandard)
    {
        _byExact = byExact;
        _byNormalized = byNormalized;
        _sourceValuesByStandard = sourceValuesByStandard;
    }

    public static ProductNormalizationMap Empty { get; } = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));

    public static ProductNormalizationMap From(IEnumerable<ProductMapping> mappings)
    {
        var ordered = mappings.Concat(SupplementalMappings)
            .OrderBy(x => ProductNormalizer.Priority(x.SourceSystem))
            .ThenBy(x => x.Id)
            .ToList();

        var exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sourceValues = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in ordered)
        {
            if (!exact.ContainsKey(mapping.SourceValue))
            {
                exact[mapping.SourceValue] = mapping.StandardProduct;
            }

            if (!sourceValues.TryGetValue(mapping.StandardProduct, out var values))
            {
                values = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mapping.StandardProduct };
                sourceValues[mapping.StandardProduct] = values;
            }
            values.Add(mapping.SourceValue);

            var key = NormalizeKey(mapping.SourceValue);
            if (key is not null && !normalized.ContainsKey(key))
            {
                normalized[key] = mapping.StandardProduct;
            }
        }

        return new ProductNormalizationMap(
            exact,
            normalized,
            sourceValues.ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Value.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase));
    }

    public string? Normalize(string? value)
    {
        var cleaned = Clean(value);
        if (cleaned is null) return null;
        if (_byExact.TryGetValue(cleaned, out var exact)) return exact;

        var key = NormalizeKey(cleaned);
        return key is not null && _byNormalized.TryGetValue(key, out var normalized)
            ? normalized
            : cleaned;
    }

    public IReadOnlyList<string> SourceValuesFor(string? standardProduct)
    {
        var normalized = Normalize(standardProduct);
        if (normalized is null) return Array.Empty<string>();
        return _sourceValuesByStandard.TryGetValue(normalized, out var values)
            ? values
            : new[] { normalized };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeKey(string? value)
    {
        var cleaned = Clean(value);
        if (cleaned is null) return null;

        var normalized = cleaned.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousWasSpace = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
                previousWasSpace = false;
            }
            else if (!previousWasSpace && builder.Length > 0)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim() is { Length: > 0 } key ? key : null;
    }
}
