using Commerce.BuildingBlocks.Application.Queries;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Orders;
using Ordering.Domain.Orders;

namespace Ordering.Infrastructure.Persistence;

/// <summary>Executes bounded Order read models against Ordering's own database.</summary>
public sealed class OrderReadStore : IOrderReadStore
{
    private readonly OrderingDbContext _db;
    /// <summary>Initializes the read store.</summary>
    /// <param name="db">Ordering database.</param>
    public OrderReadStore(OrderingDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<PagedResponse<OrderResponse>> ListAsync(PageQuery query, Guid? customerId, CancellationToken cancellationToken)
    {
        IQueryable<Order> orders = _db.Orders.AsNoTracking();
        // Scope at the database boundary: customers never download another customer's orders.
        if (customerId.HasValue) orders = orders.Where(o => o.CustomerId == customerId.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            if (Guid.TryParse(term, out Guid id)) orders = orders.Where(o => o.Id == new OrderId(id));
            else orders = orders.Where(o => EF.Functions.ILike(o.OrderNumber, $"%{term}%"));
        }
        if (query.Status is not null && Enum.TryParse(query.Status, out OrderStatus status)) orders = orders.Where(o => o.Status == status);
        if (query.From.HasValue) orders = orders.Where(o => o.CreatedAtUtc >= query.From.Value);
        if (query.To.HasValue) orders = orders.Where(o => o.CreatedAtUtc < query.To.Value);
        int total = await orders.CountAsync(cancellationToken);
        Order[] rows = await orders.Include(o => o.Items).OrderByDescending(o => o.CreatedAtUtc).ThenBy(o => o.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(cancellationToken);
        return new(rows.Select(OrderMappings.ToResponse).ToArray(), query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public async Task<OrderResponse?> GetAsync(Guid orderId, Guid? customerId, CancellationToken cancellationToken)
    {
        IQueryable<Order> query = _db.Orders.AsNoTracking().Include(o => o.Items);
        if (customerId.HasValue) query = query.Where(o => o.CustomerId == customerId.Value);
        Order? row = await query.SingleOrDefaultAsync(o => o.Id == new OrderId(orderId), cancellationToken);
        if (row is null) return null;
        OrderResponse response = OrderMappings.ToResponse(row);
        var saga = await _db.CheckoutSagas.AsNoTracking().SingleOrDefaultAsync(s => s.OrderId == orderId, cancellationToken);
        return response with { SagaStatus = saga?.Status.ToString() };
    }

    /// <inheritdoc />
    public async Task<OrderSummaryResponse> SummaryAsync(CancellationToken cancellationToken)
    {
        var counts = await _db.Orders.AsNoTracking().GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToArrayAsync(cancellationToken);
        // Sum item snapshots in SQL and keep currencies separate; no cross-service join is performed.
        var totals = await _db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Shipped || o.Status == OrderStatus.Delivered)
            .SelectMany(o => o.Items).GroupBy(i => i.UnitPrice.Currency)
            .Select(g => new CurrencyTotalResponse(g.Key, g.Sum(i => i.UnitPrice.Amount * i.Quantity)))
            .ToArrayAsync(cancellationToken);
        return new(counts.Sum(c => c.Count),
            counts.Where(c => c.Status is OrderStatus.Pending or OrderStatus.AwaitingInventory or OrderStatus.InventoryReserved or OrderStatus.AwaitingPayment).Sum(c => c.Count),
            counts.Where(c => c.Status == OrderStatus.Cancelled).Sum(c => c.Count), totals,
            counts.Select(c => new StatusCountResponse(c.Status.ToString(), c.Count)).ToArray());
    }
}
