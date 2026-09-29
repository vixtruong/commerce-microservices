using System.Globalization;
using Cart.Application.Carts;
using Cart.Contracts.Grpc;
using Grpc.Core;

namespace Cart.Api.Services;

/// <summary>Exposes a checkout-only cart snapshot to Ordering over internal gRPC.</summary>
public sealed class CartInternalGrpcService : CartInternalGrpc.CartInternalGrpcBase
{
    private readonly CartService _carts;

    /// <summary>Initializes the internal Cart gRPC service.</summary>
    /// <param name="carts">Cart application service.</param>
    public CartInternalGrpcService(CartService carts) => _carts = carts;

    /// <summary>Gets a bounded snapshot for checkout.</summary>
    /// <param name="request">Authenticated customer identifier supplied by Ordering.</param>
    /// <param name="context">Call context carrying deadline and cancellation.</param>
    /// <returns>The current cart snapshot.</returns>
    public override async Task<CartSnapshotReply> GetCartForCheckout(
        GetCartForCheckoutRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.CustomerId, out Guid customerId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Customer id is invalid."));
        }

        CustomerCart cart = await _carts.GetAsync(customerId, context.CancellationToken);
        var response = new CartSnapshotReply { CustomerId = cart.CustomerId.ToString("D") };
        response.Items.AddRange(cart.Items.Select(item => new CartItemReply
        {
            ProductId = item.ProductId.ToString("D"),
            Sku = item.Sku,
            ProductName = item.ProductName,
            UnitPrice = item.UnitPrice.ToString(CultureInfo.InvariantCulture),
            Currency = item.Currency,
            Quantity = item.Quantity
        }));
        return response;
    }
}
