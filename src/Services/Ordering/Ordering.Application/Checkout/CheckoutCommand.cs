using System.Text.Json;
using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Domain.Results;
using MediatR;
using Ordering.Contracts.IntegrationEvents;
using Ordering.Domain.Orders;

namespace Ordering.Application.Checkout;

/// <summary>Starts an eventually consistent checkout for the authenticated customer.</summary>
/// <param name="CustomerId">Authenticated JWT subject.</param>
/// <param name="IdempotencyKey">Required client retry key.</param>
/// <param name="RecipientName">Shipping recipient.</param>
/// <param name="AddressLine1">Primary address line.</param>
/// <param name="City">City.</param>
/// <param name="PostalCode">Postal code.</param>
/// <param name="CountryCode">Country code.</param>
public sealed record CheckoutCommand(
    Guid CustomerId,
    string IdempotencyKey,
    string RecipientName,
    string AddressLine1,
    string City,
    string PostalCode,
    string CountryCode) : IRequest<Result<CheckoutResponse>>;

/// <summary>Represents the accepted checkout resource.</summary>
/// <param name="OrderId">Created or replayed order identifier.</param>
/// <param name="OrderNumber">Human-readable order number.</param>
/// <param name="Status">Current local order status.</param>
public sealed record CheckoutResponse(Guid OrderId, string OrderNumber, string Status);

/// <summary>Defines checkout orchestration failures.</summary>
public static class CheckoutErrors
{
    /// <summary>Returned when the HTTP idempotency key is absent.</summary>
    public static readonly Error IdempotencyKeyRequired = Error.Validation("Ordering.IdempotencyKeyRequired", "The Idempotency-Key header is required.");
    /// <summary>Returned while another request owns the same processing key.</summary>
    public static readonly Error RequestInProgress = Error.Conflict("Ordering.RequestInProgress", "A request with this idempotency key is still processing.");
    /// <summary>Returned when the cart contains no items.</summary>
    public static readonly Error CartEmpty = Error.Validation("Ordering.CartEmpty", "The cart is empty.");
    /// <summary>Returned when Catalog cannot authoritatively validate every cart item.</summary>
    public static readonly Error ProductUnavailable = Error.Conflict("Ordering.ProductUnavailable", "One or more cart products are unavailable.");
}

/// <summary>
/// Creates Order, Saga, idempotency, and Outbox records in one local transaction; distributed work continues asynchronously.
/// </summary>
public sealed class CheckoutCommandHandler : IRequestHandler<CheckoutCommand, Result<CheckoutResponse>>
{
    private readonly IOrderRepository _orders;
    private readonly ICartCheckoutClient _cart;
    private readonly ICatalogCheckoutClient _catalog;
    private readonly IIdempotencyStore _idempotency;
    private readonly IOutbox _outbox;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initializes the checkout handler.</summary>
    /// <param name="orders">Ordering repository.</param>
    /// <param name="cart">Internal Cart client.</param>
    /// <param name="catalog">Internal Catalog client.</param>
    /// <param name="idempotency">Redis idempotency coordinator.</param>
    /// <param name="outbox">Transactional event writer.</param>
    /// <param name="unitOfWork">Ordering transaction boundary.</param>
    public CheckoutCommandHandler(
        IOrderRepository orders,
        ICartCheckoutClient cart,
        ICatalogCheckoutClient catalog,
        IIdempotencyStore idempotency,
        IOutbox outbox,
        IUnitOfWork unitOfWork)
    {
        _orders = orders;
        _cart = cart;
        _catalog = catalog;
        _idempotency = idempotency;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Validates snapshots and commits the asynchronous workflow start.</summary>
    /// <param name="request">Checkout command.</param>
    /// <param name="cancellationToken">Request-abort token.</param>
    /// <returns>An accepted resource or validation/conflict error.</returns>
    public async Task<Result<CheckoutResponse>> Handle(CheckoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)) return CheckoutErrors.IdempotencyKeyRequired;
        CheckoutIdempotencyRecord? committed = await _orders.GetIdempotencyAsync(
            request.CustomerId, request.IdempotencyKey, cancellationToken);
        if (committed is not null)
        {
            Order? existingOrder = await _orders.GetForCustomerAsync(committed.OrderId, request.CustomerId, cancellationToken);
            if (existingOrder is not null) return Map(existingOrder);
        }

        IdempotencyLease lease = await _idempotency.AcquireAsync(
            $"checkout:{request.CustomerId:D}", request.IdempotencyKey, TimeSpan.FromSeconds(30), cancellationToken);
        if (lease.State == IdempotencyLeaseState.Completed && lease.CompletedResponse is not null)
        {
            return JsonSerializer.Deserialize<CheckoutResponse>(lease.CompletedResponse)
                ?? throw new JsonException("Stored checkout response was invalid.");
        }

        if (lease.State == IdempotencyLeaseState.Processing) return CheckoutErrors.RequestInProgress;

        try
        {
            CheckoutCart cart = await _cart.GetCartAsync(request.CustomerId, cancellationToken);
            if (cart.Items.Count == 0) return CheckoutErrors.CartEmpty;
            IReadOnlyCollection<CheckoutProduct> products = await _catalog.GetProductsAsync(
                cart.Items.Select(item => item.ProductId).Distinct().ToArray(), cancellationToken);
            Dictionary<Guid, CheckoutProduct> productsById = products.ToDictionary(product => product.ProductId);
            if (cart.Items.Any(item => !productsById.TryGetValue(item.ProductId, out CheckoutProduct? product) || !product.IsActive))
            {
                return CheckoutErrors.ProductUnavailable;
            }

            Result<ShippingAddress> address = ShippingAddress.Create(
                request.RecipientName, request.AddressLine1, request.City, request.PostalCode, request.CountryCode);
            if (address.IsFailure) return address.Error;

            OrderItemSnapshot[] snapshots = cart.Items.Select(item =>
            {
                CheckoutProduct product = productsById[item.ProductId];
                return new OrderItemSnapshot(product.ProductId, product.Sku, product.Name, product.Price, product.Currency, item.Quantity);
            }).ToArray();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string orderNumber = $"ORD-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..25].ToUpperInvariant();
            Result<Order> orderResult = Order.Create(request.CustomerId, orderNumber, address.Value, snapshots, now);
            if (orderResult.IsFailure) return orderResult.Error;

            Order order = orderResult.Value;
            Guid correlationId = Guid.NewGuid();
            var saga = CheckoutSagaState.Create(order.Id.Value, correlationId, now);
            _orders.Add(order, saga, new CheckoutIdempotencyRecord(request.CustomerId, request.IdempotencyKey, order.Id.Value, now));
            var created = new OrderCreatedIntegrationEventV1(
                Guid.NewGuid(), correlationId, null, now, order.Id.Value, order.CustomerId, order.OrderNumber);
            var reservationRequested = new InventoryReservationRequestedIntegrationEventV1(
                Guid.NewGuid(), correlationId, created.MessageId, now, order.Id.Value, now.AddMinutes(15),
                order.Items.Select(item => new InventoryReservationLineV1(item.ProductId, item.Quantity)).ToArray());
            _outbox.Add(created, OrderingEventNames.OrderCreatedV1);
            _outbox.Add(reservationRequested, OrderingEventNames.InventoryReservationRequestedV1);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            CheckoutResponse response = Map(order);
            await _idempotency.CompleteAsync(
                lease, JsonSerializer.Serialize(response), TimeSpan.FromHours(24), cancellationToken);
            return response;
        }
        finally
        {
            // Token-safe release is a no-op after successful completion replaces the token.
            await _idempotency.ReleaseAsync(lease, CancellationToken.None);
        }
    }

    /// <summary>Maps an order into an accepted checkout response.</summary>
    /// <param name="order">Order aggregate.</param>
    /// <returns>Checkout resource.</returns>
    private static CheckoutResponse Map(Order order) => new(order.Id.Value, order.OrderNumber, order.Status.ToString());
}
