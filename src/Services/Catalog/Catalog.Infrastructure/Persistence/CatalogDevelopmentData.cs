using Catalog.Domain.Categories;
using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// Seeds stable local-development products used by the checkout demonstration.
/// </summary>
public static class CatalogDevelopmentData
{
    /// <summary>Stable identifier for the development MacBook product.</summary>
    public static readonly Guid MacBookProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>Stable identifier for the development keyboard product.</summary>
    public static readonly Guid KeyboardProductId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Adds deterministic products when the Catalog database is empty.</summary>
    /// <param name="context">Catalog database context.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>A task that completes after seed data is saved.</returns>
    public static async Task SeedAsync(
        CatalogDbContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await context.Categories.AnyAsync(cancellationToken))
        {
            Category electronics = Category.Create(
                "Electronics", "electronics", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new CategoryId(Guid.Parse("55555555-5555-5555-5555-555555555555"))).Value;
            context.Categories.Add(electronics);
            await context.SaveChangesAsync(cancellationToken);
        }

        if (await context.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        DateTimeOffset seededAtUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Product macBook = Product.Create(
            "MACBOOK-PRO-001", "MacBook Pro", "Development checkout product.",
            1999m, "USD", seededAtUtc, ProductId.From(MacBookProductId)).Value;
        Product keyboard = Product.Create(
            "KEYBOARD-001", "Mechanical Keyboard", "Development checkout product.",
            120m, "USD", seededAtUtc, ProductId.From(KeyboardProductId)).Value;
        macBook.Activate(seededAtUtc);
        keyboard.Activate(seededAtUtc);

        context.Products.AddRange(macBook, keyboard);
        await context.SaveChangesAsync(cancellationToken);
    }
}
