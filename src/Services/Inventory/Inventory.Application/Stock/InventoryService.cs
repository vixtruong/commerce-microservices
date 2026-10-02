using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Contracts.Messaging;
using Commerce.BuildingBlocks.Domain.Results;
using Inventory.Contracts.IntegrationEvents;
using Inventory.Domain.Stock;
using Ordering.Contracts.IntegrationEvents;

namespace Inventory.Application.Stock;

/// <summary>Defines Inventory persistence operations required by application use cases.</summary>
public interface IInventoryRepository
{
    /// <summary>Gets one stock item by product identifier.</summary>
    /// <param name="productId">Catalog product identifier.</param>
    /// <param name="tracked">Whether the aggregate will be mutated.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The stock item, or null.</returns>
    Task<StockItem?> GetByProductIdAsync(Guid productId, bool tracked, CancellationToken cancellationToken);

    /// <summary>Gets all stock items requested by a checkout.</summary>
    /// <param name="productIds">Catalog product identifiers.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>Tracked stock aggregates.</returns>
    Task<IReadOnlyCollection<StockItem>> GetByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    /// <summary>Gets stock aggregates with reservations owned by an order.</summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>Tracked aggregates containing the order reservation.</returns>
    Task<IReadOnlyCollection<StockItem>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Gets tracked stock aggregates that contain an expired pending reservation.</summary>
    /// <param name="expiresOnOrBeforeUtc">Inclusive UTC expiry cutoff.</param>
    /// <param name="maxItems">Maximum aggregate count to return in one bounded batch.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>Tracked stock aggregates with expired holds.</returns>
    Task<IReadOnlyCollection<StockItem>> GetWithExpiredReservationsAsync(
        DateTimeOffset expiresOnOrBeforeUtc,
        int maxItems,
        CancellationToken cancellationToken);

    /// <summary>Adds a new stock item.</summary>
    /// <param name="stockItem">Aggregate to add.</param>
    void Add(StockItem stockItem);
}

/// <summary>Represents Inventory state returned through REST.</summary>
/// <param name="ProductId">Catalog product identifier.</param>
/// <param name="QuantityOnHand">Physical unit count.</param>
/// <param name="ReservedQuantity">Pending held units.</param>
/// <param name="AvailableQuantity">Units available to new orders.</param>
/// <param name="Version">Optimistic concurrency version.</param>
public sealed record StockItemResponse(
    Guid ProductId,
    int QuantityOnHand,
    int ReservedQuantity,
    int AvailableQuantity,
    long Version);

/// <summary>Provides REST-facing inventory administration while keeping rules in the aggregate.</summary>
public sealed class InventoryService
{
    private readonly IInventoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initializes the inventory service.</summary>
    /// <param name="repository">Inventory repository.</param>
    /// <param name="unitOfWork">Inventory transaction boundary.</param>
    public InventoryService(IInventoryRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Gets current stock state.</summary>
    /// <param name="productId">Catalog product identifier.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The response, or null.</returns>
    public async Task<StockItemResponse?> GetAsync(Guid productId, CancellationToken cancellationToken)
    {
        StockItem? item = await _repository.GetByProductIdAsync(productId, tracked: false, cancellationToken);
        return item is null ? null : Map(item);
    }

    /// <summary>Receives physical stock and creates the stock row on first receipt.</summary>
    /// <param name="productId">Catalog product identifier.</param>
    /// <param name="quantity">Positive received quantity.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The updated state or domain error.</returns>
    public async Task<Result<StockItemResponse>> IncreaseAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        StockItem? item = await _repository.GetByProductIdAsync(productId, tracked: true, cancellationToken);
        if (item is null)
        {
            item = StockItem.Create(productId, DateTimeOffset.UtcNow);
            _repository.Add(item);
        }

        Result result = item.IncreaseStock(quantity, DateTimeOffset.UtcNow);
        if (result.IsFailure) return result.Error;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(item);
    }

    /// <summary>Maps an aggregate without exposing EF tracking state.</summary>
    /// <param name="item">Stock aggregate.</param>
    /// <returns>REST response.</returns>
    private static StockItemResponse Map(StockItem item) =>
        new(item.ProductId, item.QuantityOnHand, item.ReservedQuantity, item.AvailableQuantity, item.Version);

    /// <summary>Applies an audited adjustment using the caller's observed concurrency version.</summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="delta">Signed unit change.</param>
    /// <param name="reason">Required business reason.</param>
    /// <param name="version">Observed aggregate version.</param>
    /// <param name="actorId">Authorized administrator subject.</param>
    /// <param name="audit">Audit writer participating in the Inventory transaction.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Updated state or safe validation/conflict error.</returns>
    public async Task<Result<StockItemResponse>> AdjustAsync(Guid productId, int delta, string reason, long version,
        Guid actorId, IInventoryReadStore audit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 3 or > 500 || productId == Guid.Empty)
            return Error.Validation("Inventory.ReasonRequired", "A valid product and a reason of 3–500 characters are required.");
        StockItem? item = await _repository.GetByProductIdAsync(productId, true, cancellationToken);
        if (item is null)
        {
            item = StockItem.Create(productId, DateTimeOffset.UtcNow);
            _repository.Add(item);
        }
        if (item.Version != version) return Error.Conflict("Inventory.Concurrency", "Stock changed. Refresh before adjusting.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Result result = item.AdjustStock(delta, now);
        if (result.IsFailure) return result.Error;
        audit.AddAdjustment(new StockAdjustment { Id = Guid.NewGuid(), ProductId = productId, ActorId = actorId,
            Delta = delta, Reason = reason.Trim(), CreatedAtUtc = now });
        // The unit change and its audit record commit together, preserving a reviewable operation history.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(item);
    }
}

/// <summary>Handles idempotent reservation requests from the Ordering Saga.</summary>
public sealed class ReserveInventoryHandler : IIntegrationEventHandler<InventoryReservationRequestedIntegrationEventV1>
{
    private const string ConsumerName = "inventory.reserve-order.v1";
    private readonly IInventoryRepository _repository;
    private readonly IInbox _inbox;
    private readonly IOutbox _outbox;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initializes the reservation handler.</summary>
    /// <param name="repository">Inventory repository.</param>
    /// <param name="inbox">Durable deduplication store.</param>
    /// <param name="outbox">Transactional outcome writer.</param>
    /// <param name="unitOfWork">Inventory transaction boundary.</param>
    public ReserveInventoryHandler(IInventoryRepository repository, IInbox inbox, IOutbox outbox, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _inbox = inbox;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        InventoryReservationRequestedIntegrationEventV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        if (await _inbox.HasProcessedAsync(integrationEvent.MessageId, ConsumerName, cancellationToken)) return;

        Guid[] productIds = integrationEvent.Items.Select(line => line.ProductId).Distinct().Order().ToArray();
        IReadOnlyCollection<StockItem> items = await _repository.GetByProductIdsAsync(productIds, cancellationToken);
        Dictionary<Guid, StockItem> byProduct = items.ToDictionary(item => item.ProductId);
        InventoryReservationLineV1? unavailable = integrationEvent.Items.FirstOrDefault(line =>
            !byProduct.TryGetValue(line.ProductId, out StockItem? item) || item.AvailableQuantity < line.Quantity);

        if (unavailable is not null)
        {
            var failed = new InventoryReservationFailedIntegrationEventV1(
                Guid.NewGuid(), integrationEvent.CorrelationId, integrationEvent.MessageId, DateTimeOffset.UtcNow,
                integrationEvent.OrderId, "insufficient-stock");
            _outbox.Add(failed, InventoryEventNames.InventoryReservationFailedV1);
        }
        else
        {
            var reservations = new List<InventoryReservationReferenceV1>();
            foreach (InventoryReservationLineV1 line in integrationEvent.Items.OrderBy(line => line.ProductId))
            {
                Result<StockReservationId> result = byProduct[line.ProductId].Reserve(
                    integrationEvent.OrderId, integrationEvent.CorrelationId, line.Quantity,
                    integrationEvent.ExpiresAtUtc, DateTimeOffset.UtcNow);
                if (result.IsFailure)
                {
                    throw new InvalidOperationException(result.Error.Code);
                }

                reservations.Add(new InventoryReservationReferenceV1(line.ProductId, result.Value.Value));
            }

            var reserved = new InventoryReservedIntegrationEventV1(
                Guid.NewGuid(), integrationEvent.CorrelationId, integrationEvent.MessageId, DateTimeOffset.UtcNow,
                integrationEvent.OrderId, reservations);
            _outbox.Add(reserved, InventoryEventNames.InventoryReservedV1);
        }

        _inbox.MarkProcessed(integrationEvent.MessageId, ConsumerName);
        // Aggregate mutations, Inbox, and outgoing outcome commit atomically; concurrency conflicts are retried by RabbitMQ.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Expires abandoned inventory holds and publishes one durable outcome per affected order.</summary>
public sealed class ReservationExpirationService
{
    private readonly IInventoryRepository _repository;
    private readonly IOutbox _outbox;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initializes the reservation expiration service.</summary>
    /// <param name="repository">Inventory repository.</param>
    /// <param name="outbox">Transactional integration event writer.</param>
    /// <param name="unitOfWork">Inventory transaction boundary.</param>
    public ReservationExpirationService(IInventoryRepository repository, IOutbox outbox, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Expires one bounded batch of pending reservations whose leases elapsed.</summary>
    /// <param name="nowUtc">Current UTC time used as the inclusive expiry cutoff.</param>
    /// <param name="batchSize">Maximum stock aggregate count to process.</param>
    /// <param name="cancellationToken">Token used to cancel persistence.</param>
    /// <returns>The number of reservations expired.</returns>
    public async Task<int> ExpireBatchAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<StockItem> items = await _repository.GetWithExpiredReservationsAsync(
            nowUtc, batchSize, cancellationToken);
        var affectedOrders = new Dictionary<Guid, Guid>();
        int expiredCount = 0;

        foreach (StockItem item in items)
        {
            StockReservation[] expiredReservations = item.Reservations
                .Where(reservation => reservation.Status == StockReservationStatus.Pending
                    && reservation.ExpiresAtUtc <= nowUtc)
                .ToArray();
            foreach (StockReservation reservation in expiredReservations)
            {
                Result result = item.ExpireReservation(reservation.Id, nowUtc);
                if (result.IsFailure)
                {
                    throw new InvalidOperationException(result.Error.Code);
                }

                affectedOrders.TryAdd(reservation.OrderId, reservation.CorrelationId);
                expiredCount++;
            }
        }

        if (expiredCount == 0)
        {
            return 0;
        }

        foreach ((Guid orderId, Guid correlationId) in affectedOrders)
        {
            var expired = new InventoryReservationExpiredIntegrationEventV1(
                Guid.NewGuid(), correlationId, null, nowUtc, orderId);
            _outbox.Add(expired, InventoryEventNames.InventoryReservationExpiredV1);
        }

        // Inventory mutations and expiry outcomes must commit together to avoid silent stock release.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return expiredCount;
    }
}

/// <summary>Handles compensating release requests after payment failure.</summary>
public sealed class ReleaseInventoryHandler : IIntegrationEventHandler<InventoryReleaseRequestedIntegrationEventV1>
{
    private const string ConsumerName = "inventory.release-order.v1";
    private readonly IInventoryRepository _repository;
    private readonly IInbox _inbox;
    private readonly IOutbox _outbox;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initializes the release handler.</summary>
    /// <param name="repository">Inventory repository.</param>
    /// <param name="inbox">Durable deduplication store.</param>
    /// <param name="outbox">Transactional outcome writer.</param>
    /// <param name="unitOfWork">Inventory transaction boundary.</param>
    public ReleaseInventoryHandler(IInventoryRepository repository, IInbox inbox, IOutbox outbox, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _inbox = inbox;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task HandleAsync(InventoryReleaseRequestedIntegrationEventV1 integrationEvent, CancellationToken cancellationToken)
    {
        if (await _inbox.HasProcessedAsync(integrationEvent.MessageId, ConsumerName, cancellationToken)) return;
        IReadOnlyCollection<StockItem> items = await _repository.GetByOrderIdAsync(integrationEvent.OrderId, cancellationToken);
        foreach (StockItem item in items)
        {
            StockReservation? reservation = item.Reservations.SingleOrDefault(candidate =>
                candidate.OrderId == integrationEvent.OrderId && candidate.Status == StockReservationStatus.Pending);
            if (reservation is not null)
            {
                item.ReleaseReservation(reservation.Id, integrationEvent.Reason, DateTimeOffset.UtcNow);
            }
        }

        var released = new InventoryReleasedIntegrationEventV1(
            Guid.NewGuid(), integrationEvent.CorrelationId, integrationEvent.MessageId, DateTimeOffset.UtcNow, integrationEvent.OrderId);
        _outbox.Add(released, InventoryEventNames.InventoryReleasedV1);
        _inbox.MarkProcessed(integrationEvent.MessageId, ConsumerName);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Commits reserved stock after Ordering announces a paid order.</summary>
public sealed class ConfirmInventoryHandler : IIntegrationEventHandler<OrderPaidIntegrationEventV1>
{
    private const string ConsumerName = "inventory.confirm-paid-order.v1";
    private readonly IInventoryRepository _repository;
    private readonly IInbox _inbox;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initializes the confirmation handler.</summary>
    /// <param name="repository">Inventory repository.</param>
    /// <param name="inbox">Durable deduplication store.</param>
    /// <param name="unitOfWork">Inventory transaction boundary.</param>
    public ConfirmInventoryHandler(IInventoryRepository repository, IInbox inbox, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _inbox = inbox;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task HandleAsync(OrderPaidIntegrationEventV1 integrationEvent, CancellationToken cancellationToken)
    {
        if (await _inbox.HasProcessedAsync(integrationEvent.MessageId, ConsumerName, cancellationToken)) return;
        IReadOnlyCollection<StockItem> items = await _repository.GetByOrderIdAsync(integrationEvent.OrderId, cancellationToken);
        foreach (StockItem item in items)
        {
            StockReservation? reservation = item.Reservations.SingleOrDefault(candidate =>
                candidate.OrderId == integrationEvent.OrderId && candidate.Status == StockReservationStatus.Pending);
            if (reservation is not null) item.ConfirmReservation(reservation.Id, DateTimeOffset.UtcNow);
        }

        _inbox.MarkProcessed(integrationEvent.MessageId, ConsumerName);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
