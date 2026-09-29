using System.Globalization;
using Cart.Application.Carts;
using Catalog.Contracts.Grpc.Products;
using Grpc.Core;

namespace Cart.Infrastructure.Catalog;

/// <summary>Calls Catalog's internal read-only gRPC API with an explicit deadline.</summary>
public sealed class CatalogProductClient : ICatalogProductClient
{
    private readonly CatalogInternalGrpc.CatalogInternalGrpcClient _client;

    /// <summary>Initializes the Catalog client.</summary>
    /// <param name="client">Generated protobuf client.</param>
    public CatalogProductClient(CatalogInternalGrpc.CatalogInternalGrpcClient client) => _client = client;

    /// <inheritdoc />
    public async Task<CatalogProductSnapshot?> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        try
        {
            ProductReply reply = await _client.GetProductAsync(
                new GetProductRequest { ProductId = productId.ToString("D") },
                deadline: DateTime.UtcNow.AddSeconds(3),
                cancellationToken: cancellationToken);
            return new CatalogProductSnapshot(
                Guid.Parse(reply.Id), reply.Sku, reply.Name,
                decimal.Parse(reply.PriceAmount, CultureInfo.InvariantCulture), reply.PriceCurrency, reply.IsActive);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }
}
