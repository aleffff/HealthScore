using HealthScore.Domain;
using HealthScore.Infrastructure;

namespace HealthScore.UnitTests;

public sealed class ProductNormalizationTests
{
    [Fact]
    public void Normalizes_commercial_and_salesforce_aliases_to_standard_product()
    {
        var map = ProductNormalizationMap.From(new[]
        {
            new ProductMapping { Id = 1, Vertical = "FARMA", StandardProduct = "BIG SISTEMAS", SourceSystem = "produto_padronizado", SourceValue = "BIG SISTEMAS", UpdatedAt = DateTime.UtcNow, UpdatedBy = "test" },
            new ProductMapping { Id = 2, Vertical = "FARMA", StandardProduct = "BIG SISTEMAS", SourceSystem = "produto_comercial", SourceValue = "LINX BIG FARMA", UpdatedAt = DateTime.UtcNow, UpdatedBy = "test" },
            new ProductMapping { Id = 3, Vertical = "POSTOS", StandardProduct = "EMPÓRIO", SourceSystem = "produto_salesforce", SourceValue = "MPC", UpdatedAt = DateTime.UtcNow, UpdatedBy = "test" },
            new ProductMapping { Id = 4, Vertical = "POSTOS", StandardProduct = "MPC", SourceSystem = "produto_padronizado", SourceValue = "MPC", UpdatedAt = DateTime.UtcNow, UpdatedBy = "test" }
        });

        Assert.Equal("BIG SISTEMAS", map.Normalize("LINX BIG FARMA"));
        Assert.Equal("BIG SISTEMAS", map.Normalize("linx big farma"));
        Assert.Equal("BIG SISTEMAS", map.Normalize("Linx Farma BIG"));
        Assert.Equal("EMPÓRIO", map.Normalize("MPC"));
        Assert.Contains("LINX BIG FARMA", map.SourceValuesFor("BIG SISTEMAS"));
        Assert.Contains("LINX FARMA BIG", map.SourceValuesFor("BIG SISTEMAS"));
        Assert.Contains("BIG SISTEMAS", map.SourceValuesFor("BIG SISTEMAS"));
        Assert.Contains("MPC", map.SourceValuesFor("EMPÓRIO"));
    }

    [Fact]
    public void Keeps_unknown_product_cleaned_when_no_mapping_exists()
    {
        var map = ProductNormalizationMap.Empty;

        Assert.Equal("Produto Novo", map.Normalize("  Produto Novo  "));
    }
}
