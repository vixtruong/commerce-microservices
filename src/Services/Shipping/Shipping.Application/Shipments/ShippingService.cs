using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Contracts.Messaging;
using Commerce.BuildingBlocks.Domain.Results;
using Ordering.Contracts.IntegrationEvents;
using Shipping.Contracts.IntegrationEvents;
using Shipping.Domain.Shipments;

namespace Shipping.Application.Shipments;

/// <summary>Defines Shipping-owned aggregate persistence.</summary>
public interface IShipmentRepository
{
    /// <summary>Gets a shipment by order identifier.</summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="tracked">Whether the aggregate will be mutated.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The shipment, or null.</returns>
    Task<Shipment?> GetByOrderIdAsync(Guid orderId, bool tracked, CancellationToken cancellationToken);

    /// <summary>Gets a shipment by its identifier.</summary>
    /// <param name="shipmentId">Shipment identifier.</param>
    /// <param name="tracked">Whether the aggregate will be mutated.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The shipment, or null.</returns>
    Task<Shipment?> GetAsync(Guid shipmentId, bool tracked, CancellationToken cancellationToken);

    /// <summary>Adds a new shipment.</summary>
    /// <param name="shipment">Shipment aggregate.</param>
    void Add(Shipment shipment);
}

/// <summary>Creates exactly one shipment when an order becomes paid.</summary>
public sealed class CreateShipmentHandler : IIntegrationEventHandler<OrderPaidIntegrationEventV1>
{
    private const string ConsumerName = "shipping.create-paid-order.v1";
    private readonly IShipmentRepository _shipments;
    private readonly IInbox _inbox;
    private readonly IOutbox _outbox;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initializes the shipment consumer.</summary>
    /// <param name="shipments">Shipment repository.</param>
    /// <param name="inbox">Durable deduplication.</param>
    /// <param name="outbox">Transactional outcome writer.</param>
    /// <param name="unitOfWork">Shipping transaction boundary.</param>
    public CreateShipmentHandler(IShipmentRepository shipments, IInbox inbox, IOutbox outbox, IUnitOfWork unitOfWork)
    {
        _shipments = shipments;
        _inbox = inbox;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task HandleAsync(OrderPaidIntegrationEventV1 integrationEvent, CancellationToken cancellationToken)
    {
        if (await _inbox.HasProcessedAsync(integrationEvent.MessageId, ConsumerName, cancellationToken)) return;
        Shipment? shipment = await _shipments.GetByOrderIdAsync(integrationEvent.OrderId, tracked: true, cancellationToken);
        if (shipment is null)
        {
            var address = new DeliveryAddress(
                integrationEvent.RecipientName, integrationEvent.AddressLine1, integrationEvent.City,
                integrationEvent.PostalCode, integrationEvent.CountryCode);
            shipment = Shipment.Create(
                integrationEvent.OrderId, integrationEvent.CorrelationId, address, DateTimeOffset.UtcNow).Value;
            _shipments.Add(shipment);
            var created = new ShipmentCreatedIntegrationEventV1(
                Guid.NewGuid(), integrationEvent.CorrelationId, integrationEvent.MessageId, DateTimeOffset.UtcNow,
                shipment.OrderId, shipment.Id.Value, shipment.TrackingNumber);
            _outbox.Add(created, ShippingEventNames.ShipmentCreatedV1);
        }

        _inbox.MarkProcessed(integrationEvent.MessageId, ConsumerName);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Advances the fake development shipment lifecycle and emits delivery once.</summary>
public sealed class ShippingService
{
    private readonly IShipmentRepository _shipments;
    private readonly IOutbox _outbox;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initializes the Shipping service.</summary>
    /// <param name="shipments">Shipment repository.</param>
    /// <param name="outbox">Transactional event writer.</param>
    /// <param name="unitOfWork">Shipping transaction boundary.</param>
    public ShippingService(IShipmentRepository shipments, IOutbox outbox, IUnitOfWork unitOfWork)
    {
        _shipments = shipments;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Gets a shipment without tracking.</summary>
    /// <param name="shipmentId">Shipment identifier.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The shipment, or null.</returns>
    public Task<Shipment?> GetAsync(Guid shipmentId, CancellationToken cancellationToken) =>
        _shipments.GetAsync(shipmentId, tracked: false, cancellationToken);

    /// <summary>Gets a shipment by its source order without tracking.</summary>
    /// <param name="orderId">Ordering aggregate identifier.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The shipment, or null while asynchronous creation is pending.</returns>
    public Task<Shipment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        _shipments.GetByOrderIdAsync(orderId, tracked: false, cancellationToken);

    /// <summary>Advances one fake lifecycle step for local demonstrations.</summary>
    /// <param name="shipmentId">Shipment identifier.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The updated shipment or domain error.</returns>
    public async Task<Result<Shipment>> AdvanceAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        Shipment? shipment = await _shipments.GetAsync(shipmentId, tracked: true, cancellationToken);
        if (shipment is null) return Error.NotFound("Shipping.NotFound", "The shipment was not found.");
        Result result = shipment.Status switch
        {
            ShipmentStatus.Created => shipment.MarkReadyForPickup(DateTimeOffset.UtcNow),
            ShipmentStatus.ReadyForPickup => shipment.MarkInTransit(DateTimeOffset.UtcNow),
            ShipmentStatus.InTransit => shipment.MarkDelivered(DateTimeOffset.UtcNow),
            _ => ShipmentErrors.InvalidTransition
        };
        if (result.IsFailure) return result.Error;

        if (shipment.Status == ShipmentStatus.Delivered)
        {
            var delivered = new ShipmentDeliveredIntegrationEventV1(
                Guid.NewGuid(), shipment.CorrelationId, null, DateTimeOffset.UtcNow, shipment.OrderId, shipment.Id.Value);
            _outbox.Add(delivered, ShippingEventNames.ShipmentDeliveredV1);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return shipment;
    }
}
