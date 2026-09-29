using Ordering.Domain.Orders;

namespace Ordering.Application.Checkout;

/// <summary>Represents one item obtained from Cart over internal gRPC.</summary>
/// <param name="ProductId">Catalog product identifier.</param>
/// <param name="Quantity">Positive cart quantity.</param>
public sealed record CheckoutCartItem(Guid ProductId, int Quantity);

/// <summary>Represents the authenticated customer's checkout cart.</summary>
/// <param name="CustomerId">Customer identifier.</param>
/// <param name="Items">Cart item quantities.</param>
public sealed record CheckoutCart(Guid CustomerId, IReadOnlyCollection<CheckoutCartItem> Items);

/// <summary>Represents authoritative product data obtained from Catalog over internal gRPC.</summary>
/// <param name="ProductId">Product identifier.</param>
/// <param name="Sku">SKU snapshot.</param>
/// <param name="Name">Name snapshot.</param>
/// <param name="Price">Current price.</param>
/// <param name="Currency">Currency.</param>
/// <param name="IsActive">Whether checkout may purchase the product.</param>
public sealed record CheckoutProduct(
    Guid ProductId,
    string Sku,
    string Name,
    decimal Price,
    string Currency,
    bool IsActive);

/// <summary>Defines the bounded internal Cart query.</summary>
public interface ICartCheckoutClient
{
    /// <summary>Gets the current cart with an explicit deadline.</summary>
    /// <param name="customerId">Authenticated customer identifier.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>The checkout cart.</returns>
    Task<CheckoutCart> GetCartAsync(Guid customerId, CancellationToken cancellationToken);
}

/// <summary>Defines the bounded internal Catalog batch query.</summary>
public interface ICatalogCheckoutClient
{
    /// <summary>Gets authoritative products with an explicit deadline.</summary>
    /// <param name="productIds">Product identifiers.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>Matching product snapshots.</returns>
    Task<IReadOnlyCollection<CheckoutProduct>> GetProductsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);
}

/// <summary>Defines Order and explicit checkout Saga persistence.</summary>
public interface IOrderRepository
{
    /// <summary>Adds a new order, Saga state, and durable HTTP idempotency row.</summary>
    /// <param name="order">New order.</param>
    /// <param name="saga">New Saga state.</param>
    /// <param name="idempotency">Durable checkout key mapping.</param>
    void Add(Order order, CheckoutSagaState saga, CheckoutIdempotencyRecord idempotency);

    /// <summary>Gets an order for mutation.</summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The tracked order, or null.</returns>
    Task<Order?> GetAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Gets an order for read-only response projection.</summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="customerId">Authenticated customer identifier.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The order, or null when not owned by the customer.</returns>
    Task<Order?> GetForCustomerAsync(Guid orderId, Guid customerId, CancellationToken cancellationToken);

    /// <summary>Gets an explicit checkout Saga state for mutation.</summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The Saga state, or null.</returns>
    Task<CheckoutSagaState?> GetSagaAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Gets a previously committed checkout response by customer/key.</summary>
    /// <param name="customerId">Authenticated customer identifier.</param>
    /// <param name="key">Client idempotency key.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The durable key record, or null.</returns>
    Task<CheckoutIdempotencyRecord?> GetIdempotencyAsync(Guid customerId, string key, CancellationToken cancellationToken);
}

/// <summary>Defines explicit orchestration states for the checkout process manager.</summary>
public enum CheckoutSagaStatus
{
    /// <summary>Waiting for Inventory.</summary>
    AwaitingInventory,
    /// <summary>Waiting for Payment.</summary>
    AwaitingPayment,
    /// <summary>Payment succeeded and fulfilment may continue.</summary>
    Paid,
    /// <summary>Inventory or payment failed and compensation was started.</summary>
    Compensating,
    /// <summary>Compensation completed.</summary>
    Cancelled,
    /// <summary>Shipment exists.</summary>
    ShipmentCreated,
    /// <summary>Shipment was delivered.</summary>
    Completed
}

/// <summary>Persists the explicit Ordering-owned checkout process manager.</summary>
public sealed class CheckoutSagaState
{
    private CheckoutSagaState()
    {
    }

    private CheckoutSagaState(Guid orderId, Guid correlationId, DateTimeOffset createdAtUtc)
    {
        OrderId = orderId;
        CorrelationId = correlationId;
        Status = CheckoutSagaStatus.AwaitingInventory;
        UpdatedAtUtc = createdAtUtc;
    }

    /// <summary>Gets the Order aggregate identifier and primary key.</summary>
    public Guid OrderId { get; private set; }
    /// <summary>Gets the workflow correlation identifier.</summary>
    public Guid CorrelationId { get; private set; }
    /// <summary>Gets the process-manager state.</summary>
    public CheckoutSagaStatus Status { get; private set; }
    /// <summary>Gets the latest UTC transition time.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Creates a process manager for a newly committed order.</summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="correlationId">Checkout correlation identifier.</param>
    /// <param name="createdAtUtc">UTC creation time.</param>
    /// <returns>A new Saga state.</returns>
    public static CheckoutSagaState Create(Guid orderId, Guid correlationId, DateTimeOffset createdAtUtc) =>
        new(orderId, correlationId, createdAtUtc);

    /// <summary>Transitions the process manager to its next orchestration state.</summary>
    /// <param name="status">Next state.</param>
    /// <param name="changedAtUtc">UTC transition time.</param>
    public void MoveTo(CheckoutSagaStatus status, DateTimeOffset changedAtUtc)
    {
        Status = status;
        UpdatedAtUtc = changedAtUtc;
    }
}

/// <summary>Provides a durable relational fallback for HTTP idempotency.</summary>
public sealed class CheckoutIdempotencyRecord
{
    private CheckoutIdempotencyRecord()
    {
    }

    /// <summary>Initializes a durable checkout key mapping.</summary>
    /// <param name="customerId">Authenticated customer identifier.</param>
    /// <param name="key">Client idempotency key.</param>
    /// <param name="orderId">Created order identifier.</param>
    /// <param name="createdAtUtc">UTC creation time.</param>
    public CheckoutIdempotencyRecord(Guid customerId, string key, Guid orderId, DateTimeOffset createdAtUtc)
    {
        CustomerId = customerId;
        Key = key;
        OrderId = orderId;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Gets the authenticated customer identifier.</summary>
    public Guid CustomerId { get; private set; }
    /// <summary>Gets the client idempotency key.</summary>
    public string Key { get; private set; } = string.Empty;
    /// <summary>Gets the one order created for this key.</summary>
    public Guid OrderId { get; private set; }
    /// <summary>Gets the UTC creation time.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
