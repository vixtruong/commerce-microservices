using Catalog.Domain.Products;
using Catalog.Domain.Categories;

namespace Catalog.Domain.Tests;

/// <summary>Protects product image boundaries and stable collection identities.</summary>
public sealed class PresentationTests
{
    /// <summary>Verifies photo ordering determines the primary photo and omitted galleries retain all photos.</summary>
    [Fact]
    public void ChangePresentation_OrderedGallery_PreservesPrimaryAndLegacyEdits()
    {
        Product product = Product.Create("DEMO-1", "Keyboard", null, 100m, "USD", DateTimeOffset.UtcNow).Value;
        string[] photos = ["/api/catalog/media/front.jpg", "/api/catalog/media/back.png"];
        Assert.True(product.ChangePresentation("Keychron", null, null, null, DateTimeOffset.UtcNow, photos).IsSuccess);
        Assert.Equal(photos[0], product.ImageUrl);
        Assert.Equal(photos, product.ImageUrls);
        Assert.True(product.ChangePresentation("Keychron", product.ImageUrl, null, null, DateTimeOffset.UtcNow).IsSuccess);
        Assert.Equal(photos, product.ImageUrls);
        Assert.True(product.ChangePresentation("Keychron", product.ImageUrl, null, null, DateTimeOffset.UtcNow, []).IsSuccess);
        Assert.Null(product.ImageUrl);
        Assert.Empty(product.ImageUrls);
    }

    /// <summary>Verifies duplicate, oversized and unsafe galleries are rejected before changing the primary photo.</summary>
    [Fact]
    public void ChangePresentation_InvalidGallery_DoesNotChangeExistingPhoto()
    {
        Product product = Product.Create("DEMO-1", "Keyboard", null, 100m, "USD", DateTimeOffset.UtcNow).Value;
        const string primary = "/api/catalog/media/front.jpg";
        product.ChangePresentation(null, primary, null, null, DateTimeOffset.UtcNow);
        foreach (string[] invalid in new[] { new[] { primary, primary }, new[] { "https://example.com/photo.jpg" },
            Enumerable.Range(1, 9).Select(index => $"/api/catalog/media/photo-{index}.jpg").ToArray() })
        {
            Assert.True(product.ChangePresentation(null, primary, null, null, DateTimeOffset.UtcNow, invalid).IsFailure);
            Assert.Equal(primary, product.ImageUrl);
            Assert.Single(product.ImageUrls);
        }
    }
    /// <summary>Verifies that unsupported photo paths never replace a valid Catalog image.</summary>
    /// <param name="path">Untrusted image path.</param>
    [Theory]
    [InlineData("https://example.com/image.jpg")]
    [InlineData("//example.com/image.jpg")]
    [InlineData("/api/catalog/media/../secret.jpg")]
    [InlineData("/api/catalog/media/photo.svg")]
    public void ChangePresentation_UnsafeImage_ReturnsErrorWithoutChangingMetadata(string path)
    {
        Product product = Product.Create("DEMO-1", "Keyboard", null, 100m, "USD", DateTimeOffset.UtcNow).Value;
        var result = product.ChangePresentation("Keychron", path, "https://www.keychron.com/products/keyboard", "keyboards", DateTimeOffset.UtcNow);
        Assert.True(result.IsFailure);
        Assert.Null(product.ImageUrl);
        Assert.Null(product.Brand);
    }

    /// <summary>Verifies normalized merchandising and intentional clearing without altering price or publication.</summary>
    [Fact]
    public void ChangePresentation_ValidMetadata_NormalizesAndCanClear()
    {
        Product product = Product.Create("DEMO-1", "Keyboard", null, 100m, "USD", DateTimeOffset.UtcNow).Value;
        Assert.True(product.ChangePresentation(" Keychron ", "/api/catalog/media/demo-1.jpg", "https://www.keychron.com/products/keyboard", " KEYBOARDS ", DateTimeOffset.UtcNow).IsSuccess);
        Assert.Equal("keyboards", product.CategorySlug);
        Assert.Equal("Keychron", product.Brand);
        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.Equal(100m, product.Price.Amount);
        Assert.True(product.ChangePresentation("", "", "", "", DateTimeOffset.UtcNow).IsSuccess);
        Assert.Null(product.CategorySlug);
        Assert.Null(product.ImageUrl);
    }

    /// <summary>Verifies group renaming and disabling preserve stable product links.</summary>
    [Fact]
    public void Update_DisabledCategory_PreservesSlugAndIdentity()
    {
        Category category = Category.Create("Keyboards", " KEYBOARDS ", DateTimeOffset.UtcNow).Value;
        CategoryId id = category.Id;
        Assert.True(category.Update("Mechanical keyboards", false).IsSuccess);
        Assert.Equal("keyboards", category.Slug);
        Assert.Equal(id, category.Id);
        Assert.False(category.IsActive);
    }

    /// <summary>Verifies invalid URL keys are rejected before persistence.</summary>
    /// <param name="slug">Untrusted slug.</param>
    [Theory]
    [InlineData("../keyboards")]
    [InlineData("bad group")]
    [InlineData("-keyboards")]
    public void Create_InvalidCategorySlug_ReturnsValidationError(string slug) =>
        Assert.True(Category.Create("Keyboards", slug, DateTimeOffset.UtcNow).IsFailure);
}
