using Inventory.Domain.Stock;

namespace Inventory.Domain.Tests;

/// <summary>Protects leased Inventory holds during administrative stock adjustment.</summary>
public sealed class StockAdjustmentTests
{
    /// <summary>Verifies a negative adjustment cannot erase units reserved for checkout.</summary>
    [Fact]
    public void AdjustStock_BelowReservedUnits_RejectsWithoutChangingCounts()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        StockItem stock = StockItem.Create(Guid.NewGuid(), now);
        stock.IncreaseStock(10, now);
        stock.Reserve(Guid.NewGuid(), Guid.NewGuid(), 7, now.AddMinutes(15), now);
        long version = stock.Version;
        Assert.True(stock.AdjustStock(-4, now).IsFailure);
        Assert.Equal(10, stock.QuantityOnHand);
        Assert.Equal(7, stock.ReservedQuantity);
        Assert.Equal(version, stock.Version);
    }

    /// <summary>Verifies a valid adjustment advances concurrency and preserves reservations.</summary>
    [Fact]
    public void AdjustStock_ValidDelta_UpdatesOnlyPhysicalUnits()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        StockItem stock = StockItem.Create(Guid.NewGuid(), now);
        stock.IncreaseStock(10, now);
        stock.Reserve(Guid.NewGuid(), Guid.NewGuid(), 4, now.AddMinutes(15), now);
        long version = stock.Version;
        Assert.True(stock.AdjustStock(-6, now).IsSuccess);
        Assert.Equal(4, stock.QuantityOnHand);
        Assert.Equal(4, stock.ReservedQuantity);
        Assert.Equal(0, stock.AvailableQuantity);
        Assert.Equal(version + 1, stock.Version);
    }

    /// <summary>Verifies invalid zero and overflow adjustments preserve aggregate state.</summary>
    [Fact]
    public void AdjustStock_ZeroOrOverflow_RejectsWithoutMutation()
    {
        StockItem stock = StockItem.Create(Guid.NewGuid(), DateTimeOffset.UtcNow);
        stock.IncreaseStock(1, DateTimeOffset.UtcNow);
        Assert.True(stock.AdjustStock(0, DateTimeOffset.UtcNow).IsFailure);
        Assert.True(stock.AdjustStock(int.MaxValue, DateTimeOffset.UtcNow).IsFailure);
        Assert.Equal(1, stock.QuantityOnHand);
    }
}
