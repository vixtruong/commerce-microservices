using Commerce.BuildingBlocks.Application.Queries;
using Inventory.Application.Stock;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence;

/// <summary>Executes Inventory read models without loading full reservation aggregates.</summary>
public sealed class InventoryReadStore : IInventoryReadStore
{
    private readonly InventoryDbContext _db;
    /// <summary>Initializes Inventory read queries.</summary>
    /// <param name="db">Inventory database.</param>
    public InventoryReadStore(InventoryDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<PagedResponse<StockItemResponse>> ListAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var rows = _db.StockItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
            rows = Guid.TryParse(query.Search, out Guid id) ? rows.Where(s => s.ProductId == id) : rows.Where(s => false);
        rows = query.Status switch {
            "zero" => rows.Where(s => s.QuantityOnHand - s.ReservedQuantity == 0),
            "low" => rows.Where(s => s.QuantityOnHand - s.ReservedQuantity <= 5),
            "reserved" => rows.Where(s => s.ReservedQuantity > 0),
            _ => rows
        };
        int total = await rows.CountAsync(cancellationToken);
        var page = await rows.OrderBy(s => s.ProductId).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(s => new StockItemResponse(s.ProductId, s.QuantityOnHand, s.ReservedQuantity,
                s.QuantityOnHand - s.ReservedQuantity, s.Version)).ToArrayAsync(cancellationToken);
        return new(page, query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public async Task<InventorySummaryResponse> SummaryAsync(CancellationToken cancellationToken) =>
        await _db.StockItems.AsNoTracking().GroupBy(s => 1).Select(g => new InventorySummaryResponse(g.Count(),
            g.Sum(s => (long)s.QuantityOnHand), g.Sum(s => (long)s.ReservedQuantity),
            g.Sum(s => (long)s.QuantityOnHand - s.ReservedQuantity),
            g.Count(s => s.QuantityOnHand - s.ReservedQuantity <= 5))).SingleOrDefaultAsync(cancellationToken)
        ?? new(0, 0, 0, 0, 0);

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<ReservationResponse>> ReservationsAsync(Guid productId, CancellationToken cancellationToken) =>
        await _db.Reservations.AsNoTracking().Where(r => r.ProductId == productId).OrderByDescending(r => r.ExpiresAtUtc).Take(100)
            .Select(r => new ReservationResponse(r.OrderId, r.Quantity, r.Status.ToString(), r.ExpiresAtUtc)).ToArrayAsync(cancellationToken);

    /// <inheritdoc />
    public void AddAdjustment(StockAdjustment adjustment) => _db.StockAdjustments.Add(adjustment);
}
