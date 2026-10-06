using Commerce.BuildingBlocks.Application.Queries;
using Microsoft.EntityFrameworkCore;
using Shipping.Application.Shipments;
using Shipping.Domain.Shipments;

namespace Shipping.Infrastructure.Persistence;

/// <summary>Queries only the Shipping-owned database with bounded pagination.</summary>
public sealed class ShippingReadStore : IShippingReadStore
{
    private readonly ShippingDbContext _db;
    /// <summary>Initializes the read store.</summary>
    /// <param name="db">Shipping database.</param>
    public ShippingReadStore(ShippingDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<PagedResponse<ShipmentResponse>> ListAsync(PageQuery query, CancellationToken cancellationToken)
    {
        IQueryable<Shipment> rows = _db.Shipments.AsNoTracking();
        if (query.Status is not null && Enum.TryParse(query.Status, out ShipmentStatus status)) rows = rows.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            if (Guid.TryParse(query.Search, out Guid id)) rows = rows.Where(p => p.OrderId == id || p.Id == new ShipmentId(id));
            else rows = rows.Where(p => false);
        }
        if (query.From.HasValue) rows = rows.Where(p => p.CreatedAtUtc >= query.From.Value);
        if (query.To.HasValue) rows = rows.Where(p => p.CreatedAtUtc < query.To.Value);
        int total = await rows.CountAsync(cancellationToken);
        Shipment[] page = await rows.OrderByDescending(p => p.CreatedAtUtc).ThenBy(p => p.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(cancellationToken);
        return new(page.Select(Map).ToArray(), query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public async Task<ShipmentResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Shipment? row = await _db.Shipments.AsNoTracking().SingleOrDefaultAsync(p => p.Id == new ShipmentId(id), cancellationToken);
        return row is null ? null : Map(row);
    }

    /// <inheritdoc />
    public async Task<ShippingSummaryResponse> SummaryAsync(CancellationToken cancellationToken)
    {
        var counts = await _db.Shipments.AsNoTracking().GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToArrayAsync(cancellationToken);
        IReadOnlyCollection<CurrencyTotalResponse> amounts = Array.Empty<CurrencyTotalResponse>();
        return new(counts.Sum(c => c.Count), counts.Select(c => new ShippingStatusCountResponse(c.Status.ToString(), c.Count)).ToArray(), amounts);
    }

    /// <summary>Projects a persistence aggregate into its public read contract.</summary>
    /// <param name="p">Read-only aggregate.</param>
    /// <returns>Safe response.</returns>
    private static ShipmentResponse Map(Shipment p) => new(p.Id.Value, p.OrderId, p.TrackingNumber, p.Status.ToString(), p.CreatedAtUtc, p.UpdatedAtUtc);
}
