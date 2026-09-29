using Ordering.Domain.Orders;

namespace Ordering.Domain.Tests;

/// <summary>Verifies order creation, snapshots and guarded saga transitions.</summary>
public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly ShippingAddress Address = ShippingAddress.Create("Ada Lovelace", "1 Main St", "London", "SW1A", "GB").Value;

    /// <summary>Verifies that checkout captures product snapshots and starts inventory reservation.</summary>
    [Fact]
    public void Create_ValidItems_CreatesAwaitingInventoryOrder()
    {
        var result = Order.Create(Guid.NewGuid(), "ORD-1", Address, [Item()], Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.AwaitingInventory, result.Value.Status);
        Assert.Equal(20m, result.Value.TotalAmount);
        Assert.Single(result.Value.Items);
    }

    /// <summary>Verifies that an empty order cannot be created.</summary>
    [Fact]
    public void Create_NoItems_ReturnsValidationError()
    {
        var result = Order.Create(Guid.NewGuid(), "ORD-1", Address, [], Now);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.EmptyOrder, result.Error);
    }

    /// <summary>Verifies the happy-path transition from reservation through payment.</summary>
    [Fact]
    public void MarkPaid_AfterInventoryAndPaymentStart_Succeeds()
    {
        Order order = CreateOrder();

        Assert.True(order.MarkInventoryReserved(Now.AddMinutes(1)).IsSuccess);
        Assert.True(order.StartPayment(Now.AddMinutes(2)).IsSuccess);
        Assert.True(order.MarkPaid("tx-1", Now.AddMinutes(3)).IsSuccess);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    /// <summary>Verifies that payment cannot succeed before inventory reservation.</summary>
    [Fact]
    public void MarkPaid_BeforeInventory_ReturnsConflict()
    {
        Order order = CreateOrder();

        var result = order.MarkPaid("tx-1", Now.AddMinutes(1));

        Assert.True(result.IsFailure);
        Assert.Equal(OrderStatus.AwaitingInventory, order.Status);
    }

    /// <summary>Verifies that failure before payment cancels the order.</summary>
    [Fact]
    public void Cancel_AwaitingPayment_Succeeds()
    {
        Order order = CreateOrder();
        order.MarkInventoryReserved(Now);
        order.StartPayment(Now);

        var result = order.Cancel("payment-failed", Now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    /// <summary>Verifies that a paid order cannot be cancelled by the checkout compensation path.</summary>
    [Fact]
    public void Cancel_PaidOrder_ReturnsConflict()
    {
        Order order = CreateOrder();
        order.MarkInventoryReserved(Now);
        order.StartPayment(Now);
        order.MarkPaid("tx-1", Now);

        Assert.True(order.Cancel("late", Now).IsFailure);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    /// <summary>Verifies Shipping-driven state progression.</summary>
    [Fact]
    public void ShipmentTransitions_PaidOrder_ReachesDelivered()
    {
        Order order = CreateOrder();
        order.MarkInventoryReserved(Now);
        order.StartPayment(Now);
        order.MarkPaid("tx-1", Now);

        Assert.True(order.MarkShipped(Now.AddMinutes(1)).IsSuccess);
        Assert.True(order.MarkDelivered(Now.AddMinutes(2)).IsSuccess);
        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    /// <summary>Creates a valid order for transition-focused tests.</summary>
    /// <returns>A new order.</returns>
    private static Order CreateOrder() => Order.Create(Guid.NewGuid(), "ORD-1", Address, [Item()], Now).Value;

    /// <summary>Creates a valid product snapshot.</summary>
    /// <returns>A two-unit product snapshot.</returns>
    private static OrderItemSnapshot Item() => new(Guid.NewGuid(), "SKU-1", "Keyboard", 10m, "USD", 2);
}
