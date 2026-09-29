using Catalog.Domain.Products;

namespace Catalog.Domain.Tests;

/// <summary>Verifies core Catalog product invariants.</summary>
public sealed class ProductTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Verifies normalization and draft creation.</summary>
    [Fact]
    public void Create_ValidProduct_NormalizesSkuAndStartsDraft()
    {
        var result = Product.Create(" sku-1 ", "Keyboard", null, 10m, "usd", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("SKU-1", result.Value.Sku);
        Assert.Equal(ProductStatus.Draft, result.Value.Status);
    }

    /// <summary>Verifies that negative prices are rejected.</summary>
    [Fact]
    public void Create_NegativePrice_ReturnsValidationError()
    {
        var result = Product.Create("SKU-1", "Keyboard", null, -1m, "USD", Now);

        Assert.True(result.IsFailure);
    }

    /// <summary>Verifies explicit activation behavior.</summary>
    [Fact]
    public void Activate_DraftProduct_BecomesActive()
    {
        Product product = Product.Create("SKU-1", "Keyboard", null, 10m, "USD", Now).Value;

        var result = product.Activate(Now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(ProductStatus.Active, product.Status);
    }
}
