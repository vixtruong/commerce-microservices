using Inventory.Application.Stock;
using Inventory.Domain.Stock;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence;

/// <summary>Implements Inventory aggregate persistence in its service-owned PostgreSQL database.</summary>
public sealed class InventoryRepository : IInventoryRepository
{
    private readonly InventoryDbContext _dbContext;

    /// <summary>Initializes the repository.</summary>
    /// <param name="dbContext">Inventory context.</param>
    public InventoryRepository(InventoryDbContext dbContext) => _dbContext = dbContext;

    /// <inheritdoc />
    public Task<StockItem?> GetByProductIdAsync(Guid productId, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<StockItem> query = _dbContext.StockItems.Include(item => item.Reservations);
        if (!tracked) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.ProductId == productId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<StockItem>> GetByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken) =>
        await _dbContext.StockItems.Include(item => item.Reservations)
            .Where(item => productIds.Contains(item.ProductId))
            .OrderBy(item => item.ProductId)
            .ToArrayAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<StockItem>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        await _dbContext.StockItems.Include(item => item.Reservations)
            .Where(item => item.Reservations.Any(reservation => reservation.OrderId == orderId))
            .ToArrayAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<StockItem>> GetWithExpiredReservationsAsync(
        DateTimeOffset expiresOnOrBeforeUtc,
        int maxItems,
        CancellationToken cancellationToken) =>
        await _dbContext.StockItems.Include(item => item.Reservations)
            .Where(item => item.Reservations.Any(reservation =>
                reservation.Status == StockReservationStatus.Pending
                && reservation.ExpiresAtUtc <= expiresOnOrBeforeUtc))
            .OrderBy(item => item.Id)
            .Take(maxItems)
            .ToArrayAsync(cancellationToken);

    /// <inheritdoc />
    public void Add(StockItem stockItem) => _dbContext.StockItems.Add(stockItem);
}
