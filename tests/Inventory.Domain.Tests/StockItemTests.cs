using Inventory.Domain.Stock;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Tests;

/// <summary>Verifies Inventory aggregate invariants and persistence concurrency behavior.</summary>
public sealed class StockItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Verifies that receiving ten units into empty stock results in ten units.</summary>
    [Fact]
    public void IncreaseStock_EmptyStock_AddsQuantityOnce()
    {
        StockItem item = StockItem.Create(Guid.NewGuid(), Now);

        var result = item.IncreaseStock(10, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, item.QuantityOnHand);
    }

    /// <summary>Verifies that receiving five units into ten results in fifteen, not twenty-five.</summary>
    [Fact]
    public void IncreaseStock_ExistingStock_AddsOnlyNewQuantity()
    {
        StockItem item = StockItem.Create(Guid.NewGuid(), Now);
        item.IncreaseStock(10, Now);

        var result = item.IncreaseStock(5, Now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(15, item.QuantityOnHand);
    }

    /// <summary>Verifies that zero and negative receipts are rejected without changing state.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void IncreaseStock_InvalidQuantity_ReturnsValidationError(int quantity)
    {
        StockItem item = StockItem.Create(Guid.NewGuid(), Now);

        var result = item.IncreaseStock(quantity, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(InventoryErrors.QuantityMustBePositive, result.Error);
        Assert.Equal(0, item.QuantityOnHand);
    }

    /// <summary>Verifies that integer overflow becomes an intentional domain failure.</summary>
    [Fact]
    public void IncreaseStock_Overflow_ReturnsErrorAndPreservesQuantity()
    {
        StockItem item = StockItem.Create(Guid.NewGuid(), Now);
        item.IncreaseStock(int.MaxValue, Now);

        var result = item.IncreaseStock(1, Now.AddMinutes(1));

        Assert.True(result.IsFailure);
        Assert.Equal(InventoryErrors.QuantityOverflow, result.Error);
        Assert.Equal(int.MaxValue, item.QuantityOnHand);
    }

    /// <summary>Verifies that a repeated order request returns the same reservation without double-holding units.</summary>
    [Fact]
    public void Reserve_DuplicateOrder_IsIdempotent()
    {
        StockItem item = StockItem.Create(Guid.NewGuid(), Now);
        item.IncreaseStock(10, Now);
        Guid orderId = Guid.NewGuid();
        Guid correlationId = Guid.NewGuid();

        var first = item.Reserve(orderId, correlationId, 3, Now.AddMinutes(10), Now);
        var replay = item.Reserve(orderId, correlationId, 3, Now.AddMinutes(10), Now.AddSeconds(1));

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(3, item.ReservedQuantity);
        Assert.Single(item.Reservations);
    }

    /// <summary>Verifies that reserving more than available stock is rejected.</summary>
    [Fact]
    public void Reserve_InsufficientStock_ReturnsConflict()
    {
        StockItem item = StockItem.Create(Guid.NewGuid(), Now);
        item.IncreaseStock(1, Now);

        var result = item.Reserve(Guid.NewGuid(), Guid.NewGuid(), 2, Now.AddMinutes(10), Now);

        Assert.True(result.IsFailure);
        Assert.Equal(InventoryErrors.InsufficientStock, result.Error);
        Assert.Equal(1, item.AvailableQuantity);
    }

    /// <summary>Verifies that confirming a hold deducts on-hand and reserved quantities exactly once.</summary>
    [Fact]
    public void ConfirmReservation_PendingReservation_DeductsStock()
    {
        StockItem item = StockItem.Create(Guid.NewGuid(), Now);
        item.IncreaseStock(5, Now);
        var reservation = item.Reserve(Guid.NewGuid(), Guid.NewGuid(), 2, Now.AddMinutes(10), Now);

        var result = item.ConfirmReservation(reservation.Value, Now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, item.QuantityOnHand);
        Assert.Equal(0, item.ReservedQuantity);
    }

    /// <summary>Verifies that releasing compensation restores availability without changing physical stock.</summary>
    [Fact]
    public void ReleaseReservation_PendingReservation_RestoresAvailability()
    {
        StockItem item = StockItem.Create(Guid.NewGuid(), Now);
        item.IncreaseStock(5, Now);
        var reservation = item.Reserve(Guid.NewGuid(), Guid.NewGuid(), 2, Now.AddMinutes(10), Now);

        var result = item.ReleaseReservation(reservation.Value, "payment-failed", Now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(5, item.QuantityOnHand);
        Assert.Equal(5, item.AvailableQuantity);
    }

    /// <summary>Verifies that lease expiry restores availability without reducing physical stock.</summary>
    [Fact]
    public void ExpireReservation_PendingReservation_RestoresAvailability()
    {
        StockItem item = StockItem.Create(Guid.NewGuid(), Now);
        item.IncreaseStock(5, Now);
        var reservation = item.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), 2, Now.AddMinutes(10), Now);

        var result = item.ExpireReservation(reservation.Value, Now.AddMinutes(10));

        Assert.True(result.IsSuccess);
        Assert.Equal(5, item.QuantityOnHand);
        Assert.Equal(0, item.ReservedQuantity);
        Assert.Equal(StockReservationStatus.Expired, item.Reservations.Single().Status);
    }

    /// <summary>
    /// Verifies that two stale writers cannot both reserve the same last unit because the version is a concurrency token.
    /// </summary>
    [Fact]
    public async Task Reserve_ConcurrentLastUnit_SecondCommitThrowsConcurrencyException()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryConcurrencyContext>().UseSqlite(connection).Options;
        Guid productId = Guid.NewGuid();

        await using (var setup = new InventoryConcurrencyContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            StockItem item = StockItem.Create(productId, Now);
            item.IncreaseStock(1, Now);
            setup.StockItems.Add(item);
            await setup.SaveChangesAsync();
        }

        await using var firstContext = new InventoryConcurrencyContext(options);
        await using var secondContext = new InventoryConcurrencyContext(options);
        StockItem first = await firstContext.StockItems.SingleAsync();
        StockItem second = await secondContext.StockItems.SingleAsync();
        first.Reserve(Guid.NewGuid(), Guid.NewGuid(), 1, Now.AddMinutes(10), Now);
        second.Reserve(Guid.NewGuid(), Guid.NewGuid(), 1, Now.AddMinutes(10), Now);

        await firstContext.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    /// <summary>Minimal relational context used to prove optimistic concurrency independently of PostgreSQL availability.</summary>
    private sealed class InventoryConcurrencyContext : DbContext
    {
        /// <summary>Initializes the test context.</summary>
        /// <param name="options">SQLite context options.</param>
        public InventoryConcurrencyContext(DbContextOptions<InventoryConcurrencyContext> options) : base(options)
        {
        }

        /// <summary>Gets stock items under test.</summary>
        public DbSet<StockItem> StockItems => Set<StockItem>();

        /// <summary>Configures strongly typed keys, owned reservations, and version concurrency.</summary>
        /// <param name="modelBuilder">Model builder.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StockItem>(builder =>
            {
                builder.HasKey(item => item.Id);
                builder.Property(item => item.Id).HasConversion(id => id.Value, value => new StockItemId(value));
                builder.Property(item => item.Version).IsConcurrencyToken();
                builder.Ignore(item => item.DomainEvents);
                builder.HasMany(item => item.Reservations).WithOne().HasForeignKey(reservation => reservation.StockItemId);
            });
            modelBuilder.Entity<StockReservation>(builder =>
            {
                builder.HasKey(reservation => reservation.Id);
                builder.Property(reservation => reservation.Id).HasConversion(id => id.Value, value => new StockReservationId(value));
                builder.Property(reservation => reservation.StockItemId).HasConversion(id => id.Value, value => new StockItemId(value));
            });
        }
    }
}
