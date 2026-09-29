using Commerce.BuildingBlocks.Domain.Entities;
using Commerce.BuildingBlocks.Domain.Events;
using Commerce.BuildingBlocks.Domain.Results;

namespace Ordering.Domain.Orders;

/// <summary>Captures an immutable product and price snapshot within an order.</summary>
public sealed class OrderItem : Entity<OrderItemId>
{
    private OrderItem()
    {
    }

    internal OrderItem(
        OrderItemId id,
        Guid productId,
        string sku,
        string productName,
        Money unitPrice,
        int quantity)
        : base(id)
    {
        ProductId = productId;
        Sku = sku;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    /// <summary>Gets the Catalog product identifier.</summary>
    public Guid ProductId { get; private set; }
    /// <summary>Gets the SKU captured at checkout.</summary>
    public string Sku { get; private set; } = string.Empty;
    /// <summary>Gets the product name captured at checkout.</summary>
    public string ProductName { get; private set; } = string.Empty;
    /// <summary>Gets the unit-price snapshot.</summary>
    public Money UnitPrice { get; private set; } = null!;
    /// <summary>Gets the ordered unit count.</summary>
    public int Quantity { get; private set; }
    /// <summary>Gets the line total.</summary>
    public decimal LineTotal => UnitPrice.Amount * Quantity;
}

/// <summary>
/// Central checkout aggregate that guards historical snapshots and all workflow transitions.
/// </summary>
public sealed class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    private Order(
        OrderId id,
        Guid customerId,
        string orderNumber,
        ShippingAddress shippingAddress,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        CustomerId = customerId;
        OrderNumber = orderNumber;
        ShippingAddress = shippingAddress;
        Status = OrderStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    /// <summary>Gets the authenticated customer identifier.</summary>
    public Guid CustomerId { get; private set; }
    /// <summary>Gets the human-readable order number.</summary>
    public string OrderNumber { get; private set; } = string.Empty;
    /// <summary>Gets the guarded workflow state.</summary>
    public OrderStatus Status { get; private set; }
    /// <summary>Gets the delivery-address snapshot.</summary>
    public ShippingAddress ShippingAddress { get; private set; } = null!;
    /// <summary>Gets the UTC creation time.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }
    /// <summary>Gets the UTC time of the latest transition.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    /// <summary>Gets the cancellation reason when cancelled.</summary>
    public string? CancellationReason { get; private set; }
    /// <summary>Gets order-item snapshots.</summary>
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    /// <summary>Gets the order total.</summary>
    public decimal TotalAmount => _items.Sum(item => item.LineTotal);
    /// <summary>Gets the shared item currency.</summary>
    public string Currency => _items.Count == 0 ? string.Empty : _items[0].UnitPrice.Currency;

    /// <summary>Creates a validated order and starts its inventory phase.</summary>
    /// <param name="customerId">Authenticated customer identifier.</param>
    /// <param name="orderNumber">Human-readable unique number.</param>
    /// <param name="shippingAddress">Validated address snapshot.</param>
    /// <param name="items">Validated product snapshots.</param>
    /// <param name="createdAtUtc">UTC creation time.</param>
    /// <returns>A new order or validation error.</returns>
    public static Result<Order> Create(
        Guid customerId,
        string orderNumber,
        ShippingAddress shippingAddress,
        IEnumerable<OrderItemSnapshot> items,
        DateTimeOffset createdAtUtc)
    {
        OrderItemSnapshot[] snapshots = items.ToArray();
        if (customerId == Guid.Empty || string.IsNullOrWhiteSpace(orderNumber) || snapshots.Length == 0)
        {
            return OrderErrors.EmptyOrder;
        }

        var order = new Order(OrderId.New(), customerId, orderNumber.Trim(), shippingAddress, createdAtUtc);
        foreach (OrderItemSnapshot snapshot in snapshots)
        {
            Result<Money> price = Money.Create(snapshot.UnitPrice, snapshot.Currency);
            if (snapshot.ProductId == Guid.Empty || string.IsNullOrWhiteSpace(snapshot.Sku) ||
                string.IsNullOrWhiteSpace(snapshot.ProductName) || snapshot.Quantity <= 0 || price.IsFailure)
            {
                return OrderErrors.InvalidItem;
            }

            if (order._items.Count > 0 && order.Currency != price.Value.Currency)
            {
                return OrderErrors.InvalidMoney;
            }

            order._items.Add(new OrderItem(
                OrderItemId.New(), snapshot.ProductId, snapshot.Sku.Trim(), snapshot.ProductName.Trim(), price.Value, snapshot.Quantity));
        }

        order.Status = OrderStatus.AwaitingInventory;
        order.RaiseDomainEvent(new OrderCreatedDomainEvent(order.Id.Value, order.CustomerId, order.TotalAmount, order.Currency));
        return order;
    }

    /// <summary>Records successful inventory reservation.</summary>
    /// <param name="changedAtUtc">UTC transition time.</param>
    /// <returns>A success or invalid transition.</returns>
    public Result MarkInventoryReserved(DateTimeOffset changedAtUtc) =>
        Transition(OrderStatus.AwaitingInventory, OrderStatus.InventoryReserved, changedAtUtc);

    /// <summary>Starts asynchronous payment after inventory succeeds.</summary>
    /// <param name="changedAtUtc">UTC transition time.</param>
    /// <returns>A success or invalid transition.</returns>
    public Result StartPayment(DateTimeOffset changedAtUtc) =>
        Transition(OrderStatus.InventoryReserved, OrderStatus.AwaitingPayment, changedAtUtc);

    /// <summary>Marks payment successful and raises an in-process domain event.</summary>
    /// <param name="transactionReference">Provider transaction reference.</param>
    /// <param name="changedAtUtc">UTC transition time.</param>
    /// <returns>A success or invalid transition.</returns>
    public Result MarkPaid(string transactionReference, DateTimeOffset changedAtUtc)
    {
        Result result = Transition(OrderStatus.AwaitingPayment, OrderStatus.Paid, changedAtUtc);
        if (result.IsSuccess)
        {
            RaiseDomainEvent(new OrderPaidDomainEvent(Id.Value, transactionReference));
        }

        return result;
    }

    /// <summary>Cancels an unfinished order after inventory or payment failure.</summary>
    /// <param name="reason">Auditable cancellation reason.</param>
    /// <param name="changedAtUtc">UTC transition time.</param>
    /// <returns>A success or invalid transition.</returns>
    public Result Cancel(string reason, DateTimeOffset changedAtUtc)
    {
        if (Status is OrderStatus.Paid or OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered)
        {
            return OrderErrors.InvalidTransition;
        }

        if (Status == OrderStatus.Cancelled)
        {
            return Result.Success();
        }

        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        UpdatedAtUtc = changedAtUtc;
        RaiseDomainEvent(new OrderCancelledDomainEvent(Id.Value, reason));
        return Result.Success();
    }

    /// <summary>Marks a paid order as being prepared.</summary>
    /// <param name="changedAtUtc">UTC transition time.</param>
    /// <returns>A success or invalid transition.</returns>
    public Result StartProcessing(DateTimeOffset changedAtUtc) =>
        Transition(OrderStatus.Paid, OrderStatus.Processing, changedAtUtc);

    /// <summary>Marks an order as shipped.</summary>
    /// <param name="changedAtUtc">UTC transition time.</param>
    /// <returns>A success or invalid transition.</returns>
    public Result MarkShipped(DateTimeOffset changedAtUtc)
    {
        if (Status == OrderStatus.Paid)
        {
            Status = OrderStatus.Processing;
        }

        return Transition(OrderStatus.Processing, OrderStatus.Shipped, changedAtUtc);
    }

    /// <summary>Marks a shipped order as delivered.</summary>
    /// <param name="changedAtUtc">UTC transition time.</param>
    /// <returns>A success or invalid transition.</returns>
    public Result MarkDelivered(DateTimeOffset changedAtUtc) =>
        Transition(OrderStatus.Shipped, OrderStatus.Delivered, changedAtUtc);

    /// <summary>Applies one exact state transition.</summary>
    /// <param name="expected">Required current state.</param>
    /// <param name="next">Target state.</param>
    /// <param name="changedAtUtc">UTC transition time.</param>
    /// <returns>A success or invalid transition.</returns>
    private Result Transition(OrderStatus expected, OrderStatus next, DateTimeOffset changedAtUtc)
    {
        if (Status != expected)
        {
            return OrderErrors.InvalidTransition;
        }

        Status = next;
        UpdatedAtUtc = changedAtUtc;
        return Result.Success();
    }
}

/// <summary>Provides primitive product data used to create a historical order item.</summary>
/// <param name="ProductId">Catalog product identifier.</param>
/// <param name="Sku">SKU snapshot.</param>
/// <param name="ProductName">Name snapshot.</param>
/// <param name="UnitPrice">Authoritative unit price.</param>
/// <param name="Currency">Currency snapshot.</param>
/// <param name="Quantity">Positive ordered unit count.</param>
public sealed record OrderItemSnapshot(
    Guid ProductId,
    string Sku,
    string ProductName,
    decimal UnitPrice,
    string Currency,
    int Quantity);

/// <summary>Raised when an order is created and awaits inventory.</summary>
/// <param name="OrderId">Order identifier.</param>
/// <param name="CustomerId">Customer identifier.</param>
/// <param name="Amount">Order total.</param>
/// <param name="Currency">Order currency.</param>
public sealed record OrderCreatedDomainEvent(Guid OrderId, Guid CustomerId, decimal Amount, string Currency) : DomainEvent;

/// <summary>Raised when payment succeeds.</summary>
/// <param name="OrderId">Order identifier.</param>
/// <param name="TransactionReference">Provider transaction reference.</param>
public sealed record OrderPaidDomainEvent(Guid OrderId, string TransactionReference) : DomainEvent;

/// <summary>Raised when the saga cancels an order.</summary>
/// <param name="OrderId">Order identifier.</param>
/// <param name="Reason">Cancellation reason.</param>
public sealed record OrderCancelledDomainEvent(Guid OrderId, string Reason) : DomainEvent;
