using Commerce.BuildingBlocks.Domain.Results;
using MediatR;
using Ordering.Application.Checkout;
using Ordering.Domain.Orders;

namespace Ordering.Application.Orders;

/// <summary>Gets one order owned by the authenticated customer.</summary>
/// <param name="OrderId">Order identifier.</param>
/// <param name="CustomerId">Authenticated customer identifier.</param>
public sealed record GetOrderQuery(Guid OrderId, Guid CustomerId) : IRequest<Result<OrderResponse>>;

/// <summary>Represents an order item snapshot returned to its customer.</summary>
/// <param name="ProductId">Product identifier.</param>
/// <param name="Sku">Historical SKU.</param>
/// <param name="ProductName">Historical product name.</param>
/// <param name="UnitPrice">Historical unit price.</param>
/// <param name="Currency">Currency.</param>
/// <param name="Quantity">Quantity.</param>
public sealed record OrderItemResponse(
    Guid ProductId,
    string Sku,
    string ProductName,
    decimal UnitPrice,
    string Currency,
    int Quantity);

/// <summary>Represents customer-visible order state.</summary>
/// <param name="Id">Order identifier.</param>
/// <param name="OrderNumber">Human-readable number.</param>
/// <param name="Status">Current eventually consistent state.</param>
/// <param name="TotalAmount">Order total.</param>
/// <param name="Currency">Currency.</param>
/// <param name="Items">Historical product snapshots.</param>
/// <param name="CreatedAtUtc">UTC creation time.</param>
/// <param name="UpdatedAtUtc">UTC latest transition time.</param>
public sealed record OrderResponse(
    Guid Id,
    string OrderNumber,
    string Status,
    decimal TotalAmount,
    string Currency,
    IReadOnlyCollection<OrderItemResponse> Items,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>Handles authenticated order queries.</summary>
public sealed class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, Result<OrderResponse>>
{
    private readonly IOrderRepository _orders;

    /// <summary>Initializes the query handler.</summary>
    /// <param name="orders">Ordering repository.</param>
    public GetOrderQueryHandler(IOrderRepository orders) => _orders = orders;

    /// <summary>Gets and projects an order without exposing tracked entities.</summary>
    /// <param name="request">Order query.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The order or not-found error.</returns>
    public async Task<Result<OrderResponse>> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        Order? order = await _orders.GetForCustomerAsync(request.OrderId, request.CustomerId, cancellationToken);
        if (order is null) return Error.NotFound("Ordering.OrderNotFound", "The order was not found.");
        return new OrderResponse(
            order.Id.Value,
            order.OrderNumber,
            order.Status.ToString(),
            order.TotalAmount,
            order.Currency,
            order.Items.Select(item => new OrderItemResponse(
                item.ProductId, item.Sku, item.ProductName, item.UnitPrice.Amount, item.UnitPrice.Currency, item.Quantity)).ToArray(),
            order.CreatedAtUtc,
            order.UpdatedAtUtc);
    }
}
