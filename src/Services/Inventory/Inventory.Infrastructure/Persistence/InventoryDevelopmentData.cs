using Inventory.Domain.Stock;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence;

/// <summary>
/// Seeds stable local-development stock aligned with Catalog development product identifiers.
/// </summary>
public static class InventoryDevelopmentData
{
    private static readonly Guid MacBookProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid KeyboardProductId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Adds deterministic stock when the Inventory database is empty.</summary>
    /// <param name="context">Inventory database context.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>A task that completes after stock is saved.</returns>
    public static async Task SeedAsync(
        InventoryDbContext context,
        CancellationToken cancellationToken = default)
    {
        if (await context.StockItems.AnyAsync(cancellationToken))
        {
            return;
        }

        DateTimeOffset seededAtUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        StockItem macBook = StockItem.Create(
            MacBookProductId, seededAtUtc,
            new StockItemId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
        StockItem keyboard = StockItem.Create(
            KeyboardProductId, seededAtUtc,
            new StockItemId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")));
        macBook.IncreaseStock(10, seededAtUtc);
        keyboard.IncreaseStock(50, seededAtUtc);

        context.StockItems.AddRange(macBook, keyboard);
        await context.SaveChangesAsync(cancellationToken);
    }
}
