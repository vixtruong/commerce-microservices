using System.Globalization;
using Cart.Contracts.Grpc;
using Catalog.Contracts.Grpc.Products;
using Ordering.Application.Checkout;

namespace Ordering.Infrastructure.Grpc;

/// <summary>Reads a customer's checkout cart from the internal Cart contract.</summary>
public sealed class CartCheckoutClient : ICartCheckoutClient
{
    private readonly CartInternalGrpc.CartInternalGrpcClient _client;

    /// <summary>Initializes the Cart client.</summary>
    /// <param name="client">Generated Cart gRPC client.</param>
    public CartCheckoutClient(CartInternalGrpc.CartInternalGrpcClient client) => _client = client;

    /// <inheritdoc />
    public async Task<CheckoutCart> GetCartAsync(Guid customerId, CancellationToken cancellationToken)
    {
        CartSnapshotReply response = await _client.GetCartForCheckoutAsync(
            new GetCartForCheckoutRequest { CustomerId = customerId.ToString("D") },
            deadline: DateTime.UtcNow.AddSeconds(3),
            cancellationToken: cancellationToken);
        return new CheckoutCart(customerId, response.Items.Select(item =>
            new CheckoutCartItem(Guid.Parse(item.ProductId), item.Quantity)).ToArray());
    }
}

/// <summary>Reads authoritative product and price snapshots from Catalog's internal batch contract.</summary>
public sealed class CatalogCheckoutClient : ICatalogCheckoutClient
{
    private readonly CatalogInternalGrpc.CatalogInternalGrpcClient _client;

    /// <summary>Initializes the Catalog client.</summary>
    /// <param name="client">Generated Catalog gRPC client.</param>
    public CatalogCheckoutClient(CatalogInternalGrpc.CatalogInternalGrpcClient client) => _client = client;

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<CheckoutProduct>> GetProductsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var request = new GetProductsRequest();
        request.ProductIds.AddRange(productIds.Select(id => id.ToString("D")));
        GetProductsReply response = await _client.GetProductsAsync(
            request,
            deadline: DateTime.UtcNow.AddSeconds(3),
            cancellationToken: cancellationToken);
        return response.Products.Select(product => new CheckoutProduct(
            Guid.Parse(product.Id), product.Sku, product.Name,
            decimal.Parse(product.PriceAmount, CultureInfo.InvariantCulture), product.PriceCurrency, product.IsActive)).ToArray();
    }
}
