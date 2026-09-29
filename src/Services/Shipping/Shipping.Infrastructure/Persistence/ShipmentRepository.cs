using Microsoft.EntityFrameworkCore;
using Shipping.Application.Shipments;
using Shipping.Domain.Shipments;

namespace Shipping.Infrastructure.Persistence;

/// <summary>Implements Shipping aggregate persistence.</summary>
public sealed class ShipmentRepository : IShipmentRepository
{
    private readonly ShippingDbContext _dbContext;

    /// <summary>Initializes the repository.</summary>
    /// <param name="dbContext">Shipping context.</param>
    public ShipmentRepository(ShippingDbContext dbContext) => _dbContext = dbContext;

    /// <inheritdoc />
    public Task<Shipment?> GetByOrderIdAsync(Guid orderId, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<Shipment> query = _dbContext.Shipments;
        if (!tracked) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(shipment => shipment.OrderId == orderId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Shipment?> GetAsync(Guid shipmentId, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<Shipment> query = _dbContext.Shipments;
        if (!tracked) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(shipment => shipment.Id == new ShipmentId(shipmentId), cancellationToken);
    }

    /// <inheritdoc />
    public void Add(Shipment shipment) => _dbContext.Shipments.Add(shipment);
}
