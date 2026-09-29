using Microsoft.EntityFrameworkCore;
using Ordering.Application.Checkout;
using Ordering.Domain.Orders;

namespace Ordering.Infrastructure.Persistence;

/// <summary>Implements Order and checkout process-manager persistence.</summary>
public sealed class OrderRepository : IOrderRepository
{
    private readonly OrderingDbContext _dbContext;

    /// <summary>Initializes the repository.</summary>
    /// <param name="dbContext">Ordering context.</param>
    public OrderRepository(OrderingDbContext dbContext) => _dbContext = dbContext;

    /// <inheritdoc />
    public void Add(Order order, CheckoutSagaState saga, CheckoutIdempotencyRecord idempotency)
    {
        _dbContext.Orders.Add(order);
        _dbContext.CheckoutSagas.Add(saga);
        _dbContext.CheckoutIdempotency.Add(idempotency);
    }

    /// <inheritdoc />
    public Task<Order?> GetAsync(Guid orderId, CancellationToken cancellationToken) =>
        _dbContext.Orders.Include(order => order.Items)
            .SingleOrDefaultAsync(order => order.Id == new OrderId(orderId), cancellationToken);

    /// <inheritdoc />
    public Task<Order?> GetForCustomerAsync(Guid orderId, Guid customerId, CancellationToken cancellationToken) =>
        _dbContext.Orders.AsNoTracking().Include(order => order.Items)
            .SingleOrDefaultAsync(order => order.Id == new OrderId(orderId) && order.CustomerId == customerId, cancellationToken);

    /// <inheritdoc />
    public Task<CheckoutSagaState?> GetSagaAsync(Guid orderId, CancellationToken cancellationToken) =>
        _dbContext.CheckoutSagas.SingleOrDefaultAsync(saga => saga.OrderId == orderId, cancellationToken);

    /// <inheritdoc />
    public Task<CheckoutIdempotencyRecord?> GetIdempotencyAsync(
        Guid customerId,
        string key,
        CancellationToken cancellationToken) =>
        _dbContext.CheckoutIdempotency.AsNoTracking()
            .SingleOrDefaultAsync(record => record.CustomerId == customerId && record.Key == key, cancellationToken);
}
