using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Contracts.Messaging;
using Inventory.Contracts.IntegrationEvents;
using Ordering.Contracts.IntegrationEvents;
using Ordering.Domain.Orders;
using Payment.Contracts.IntegrationEvents;
using Shipping.Contracts.IntegrationEvents;

namespace Ordering.Application.Checkout;

/// <summary>Orchestrates the explicit checkout Saga from durable integration outcomes.</summary>
public sealed class CheckoutSagaHandler :
    IIntegrationEventHandler<InventoryReservedIntegrationEventV1>,
    IIntegrationEventHandler<InventoryReservationFailedIntegrationEventV1>,
    IIntegrationEventHandler<InventoryReservationExpiredIntegrationEventV1>,
    IIntegrationEventHandler<InventoryReleasedIntegrationEventV1>,
    IIntegrationEventHandler<PaymentSucceededIntegrationEventV1>,
    IIntegrationEventHandler<PaymentFailedIntegrationEventV1>,
    IIntegrationEventHandler<ShipmentCreatedIntegrationEventV1>,
    IIntegrationEventHandler<ShipmentDeliveredIntegrationEventV1>
{
    private readonly IOrderRepository _orders;
    private readonly IInbox _inbox;
    private readonly IOutbox _outbox;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initializes the Saga handler.</summary>
    /// <param name="orders">Order and Saga repository.</param>
    /// <param name="inbox">Durable message deduplication.</param>
    /// <param name="outbox">Transactional next-step writer.</param>
    /// <param name="unitOfWork">Ordering transaction boundary.</param>
    public CheckoutSagaHandler(IOrderRepository orders, IInbox inbox, IOutbox outbox, IUnitOfWork unitOfWork)
    {
        _orders = orders;
        _inbox = inbox;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task HandleAsync(InventoryReservedIntegrationEventV1 integrationEvent, CancellationToken cancellationToken)
    {
        const string consumer = "ordering.inventory-reserved.v1";
        if (await _inbox.HasProcessedAsync(integrationEvent.MessageId, consumer, cancellationToken)) return;
        (Order order, CheckoutSagaState saga) = await LoadAsync(integrationEvent.OrderId, cancellationToken);
        if (order.Status == OrderStatus.AwaitingInventory)
        {
            order.MarkInventoryReserved(DateTimeOffset.UtcNow);
            order.StartPayment(DateTimeOffset.UtcNow);
            saga.MoveTo(CheckoutSagaStatus.AwaitingPayment, DateTimeOffset.UtcNow);
            var payment = new PaymentRequestedIntegrationEventV1(
                Guid.NewGuid(), integrationEvent.CorrelationId, integrationEvent.MessageId, DateTimeOffset.UtcNow,
                order.Id.Value, order.TotalAmount, order.Currency);
            _outbox.Add(payment, OrderingEventNames.PaymentRequestedV1);
        }

        await CommitAsync(integrationEvent.MessageId, consumer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(InventoryReservationFailedIntegrationEventV1 integrationEvent, CancellationToken cancellationToken)
    {
        const string consumer = "ordering.inventory-failed.v1";
        if (await _inbox.HasProcessedAsync(integrationEvent.MessageId, consumer, cancellationToken)) return;
        (Order order, CheckoutSagaState saga) = await LoadAsync(integrationEvent.OrderId, cancellationToken);
        if (order.Status != OrderStatus.Cancelled)
        {
            order.Cancel(integrationEvent.Reason, DateTimeOffset.UtcNow);
            saga.MoveTo(CheckoutSagaStatus.Cancelled, DateTimeOffset.UtcNow);
            AddCancelled(order, saga, integrationEvent.MessageId, integrationEvent.Reason);
        }

        // Payment is deliberately not requested on this branch.
        await CommitAsync(integrationEvent.MessageId, consumer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(InventoryReservationExpiredIntegrationEventV1 integrationEvent, CancellationToken cancellationToken)
    {
        const string consumer = "ordering.inventory-expired.v1";
        if (await _inbox.HasProcessedAsync(integrationEvent.MessageId, consumer, cancellationToken)) return;
        (Order order, CheckoutSagaState saga) = await LoadAsync(integrationEvent.OrderId, cancellationToken);
        if (order.Status is OrderStatus.AwaitingInventory or OrderStatus.AwaitingPayment)
        {
            const string reason = "inventory-reservation-expired";
            order.Cancel(reason, DateTimeOffset.UtcNow);
            saga.MoveTo(CheckoutSagaStatus.Cancelled, DateTimeOffset.UtcNow);
            AddCancelled(order, saga, integrationEvent.MessageId, reason);
        }

        await CommitAsync(integrationEvent.MessageId, consumer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(InventoryReleasedIntegrationEventV1 integrationEvent, CancellationToken cancellationToken)
    {
        const string consumer = "ordering.inventory-released.v1";
        if (await _inbox.HasProcessedAsync(integrationEvent.MessageId, consumer, cancellationToken)) return;
        (_, CheckoutSagaState saga) = await LoadAsync(integrationEvent.OrderId, cancellationToken);
        if (saga.Status == CheckoutSagaStatus.Compensating)
        {
            saga.MoveTo(CheckoutSagaStatus.Cancelled, DateTimeOffset.UtcNow);
        }

        await CommitAsync(integrationEvent.MessageId, consumer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(PaymentSucceededIntegrationEventV1 integrationEvent, CancellationToken cancellationToken)
    {
        const string consumer = "ordering.payment-succeeded.v1";
        if (await _inbox.HasProcessedAsync(integrationEvent.MessageId, consumer, cancellationToken)) return;
        (Order order, CheckoutSagaState saga) = await LoadAsync(integrationEvent.OrderId, cancellationToken);
        if (order.Status == OrderStatus.AwaitingPayment)
        {
            order.MarkPaid(integrationEvent.TransactionReference, DateTimeOffset.UtcNow);
            saga.MoveTo(CheckoutSagaStatus.Paid, DateTimeOffset.UtcNow);
            var paid = new OrderPaidIntegrationEventV1(
                Guid.NewGuid(), integrationEvent.CorrelationId, integrationEvent.MessageId, DateTimeOffset.UtcNow,
                order.Id.Value, order.ShippingAddress.RecipientName, order.ShippingAddress.Line1,
                order.ShippingAddress.City, order.ShippingAddress.PostalCode, order.ShippingAddress.CountryCode);
            _outbox.Add(paid, OrderingEventNames.OrderPaidV1);
        }

        await CommitAsync(integrationEvent.MessageId, consumer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(PaymentFailedIntegrationEventV1 integrationEvent, CancellationToken cancellationToken)
    {
        const string consumer = "ordering.payment-failed.v1";
        if (await _inbox.HasProcessedAsync(integrationEvent.MessageId, consumer, cancellationToken)) return;
        (Order order, CheckoutSagaState saga) = await LoadAsync(integrationEvent.OrderId, cancellationToken);
        if (order.Status != OrderStatus.Cancelled)
        {
            order.Cancel(integrationEvent.Reason, DateTimeOffset.UtcNow);
            saga.MoveTo(CheckoutSagaStatus.Compensating, DateTimeOffset.UtcNow);
            var release = new InventoryReleaseRequestedIntegrationEventV1(
                Guid.NewGuid(), integrationEvent.CorrelationId, integrationEvent.MessageId, DateTimeOffset.UtcNow,
                order.Id.Value, integrationEvent.Reason);
            _outbox.Add(release, OrderingEventNames.InventoryReleaseRequestedV1);
            AddCancelled(order, saga, integrationEvent.MessageId, integrationEvent.Reason);
        }

        await CommitAsync(integrationEvent.MessageId, consumer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(ShipmentCreatedIntegrationEventV1 integrationEvent, CancellationToken cancellationToken)
    {
        const string consumer = "ordering.shipment-created.v1";
        if (await _inbox.HasProcessedAsync(integrationEvent.MessageId, consumer, cancellationToken)) return;
        (Order order, CheckoutSagaState saga) = await LoadAsync(integrationEvent.OrderId, cancellationToken);
        if (order.Status == OrderStatus.Paid)
        {
            order.RecordShipment(integrationEvent.ShipmentId, integrationEvent.TrackingNumber);
            order.MarkShipped(DateTimeOffset.UtcNow);
            saga.MoveTo(CheckoutSagaStatus.ShipmentCreated, DateTimeOffset.UtcNow);
        }

        await CommitAsync(integrationEvent.MessageId, consumer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(ShipmentDeliveredIntegrationEventV1 integrationEvent, CancellationToken cancellationToken)
    {
        const string consumer = "ordering.shipment-delivered.v1";
        if (await _inbox.HasProcessedAsync(integrationEvent.MessageId, consumer, cancellationToken)) return;
        (Order order, CheckoutSagaState saga) = await LoadAsync(integrationEvent.OrderId, cancellationToken);
        if (order.Status == OrderStatus.Shipped)
        {
            order.MarkDelivered(DateTimeOffset.UtcNow);
            saga.MoveTo(CheckoutSagaStatus.Completed, DateTimeOffset.UtcNow);
        }

        await CommitAsync(integrationEvent.MessageId, consumer, cancellationToken);
    }

    /// <summary>Loads the aggregate and process manager required for one atomic transition.</summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The tracked order and Saga.</returns>
    private async Task<(Order Order, CheckoutSagaState Saga)> LoadAsync(Guid orderId, CancellationToken cancellationToken)
    {
        Order order = await _orders.GetAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {orderId} was not found for Saga processing.");
        CheckoutSagaState saga = await _orders.GetSagaAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Checkout Saga {orderId} was not found.");
        return (order, saga);
    }

    /// <summary>Adds a public cancellation outcome.</summary>
    /// <param name="order">Cancelled order.</param>
    /// <param name="saga">Checkout Saga.</param>
    /// <param name="causationId">Failure message identifier.</param>
    /// <param name="reason">Cancellation reason.</param>
    private void AddCancelled(Order order, CheckoutSagaState saga, Guid causationId, string reason)
    {
        var cancelled = new OrderCancelledIntegrationEventV1(
            Guid.NewGuid(), saga.CorrelationId, causationId, DateTimeOffset.UtcNow, order.Id.Value, reason);
        _outbox.Add(cancelled, OrderingEventNames.OrderCancelledV1);
    }

    /// <summary>Marks the inbox row and commits aggregate, Saga, Inbox, and Outbox atomically.</summary>
    /// <param name="messageId">Handled message identifier.</param>
    /// <param name="consumer">Stable handler identity.</param>
    /// <param name="cancellationToken">Token used to cancel persistence.</param>
    /// <returns>A task that completes after commit.</returns>
    private async Task CommitAsync(Guid messageId, string consumer, CancellationToken cancellationToken)
    {
        _inbox.MarkProcessed(messageId, consumer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
