using System.Globalization;
using Catalog.Application.Abstractions;
using Catalog.Contracts.Grpc.Products;
using Catalog.Domain.Products;
using Grpc.Core;

namespace Catalog.Api.Services;

/// <summary>Exposes authoritative product snapshots to trusted internal services.</summary>
public sealed class CatalogInternalGrpcService : CatalogInternalGrpc.CatalogInternalGrpcBase
{
    private readonly IProductRepository _products;

    /// <summary>Initializes the internal Catalog gRPC service.</summary>
    /// <param name="products">Read-only product repository.</param>
    public CatalogInternalGrpcService(IProductRepository products) => _products = products;

    /// <summary>Gets one authoritative product snapshot.</summary>
    /// <param name="request">Product identifier request.</param>
    /// <param name="context">Call context carrying deadline and cancellation.</param>
    /// <returns>The requested product snapshot.</returns>
    public override async Task<ProductReply> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ProductId, out Guid parsed))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Product id is invalid."));
        }

        Product? product = await _products.GetByIdAsync(ProductId.From(parsed), context.CancellationToken);
        return product is null
            ? throw new RpcException(new Status(StatusCode.NotFound, "Product was not found."))
            : Map(product);
    }

    /// <summary>Gets a checkout batch without exposing Catalog domain objects.</summary>
    /// <param name="request">Requested product identifiers.</param>
    /// <param name="context">Call context carrying deadline and cancellation.</param>
    /// <returns>Matching product snapshots.</returns>
    public override async Task<GetProductsReply> GetProducts(GetProductsRequest request, ServerCallContext context)
    {
        ProductId[] ids = request.ProductIds
            .Select(value => Guid.TryParse(value, out Guid parsed) ? ProductId.From(parsed) : default)
            .Where(id => id.Value != Guid.Empty)
            .Distinct()
            .ToArray();
        IReadOnlyCollection<Product> products = await _products.GetByIdsAsync(ids, context.CancellationToken);
        var response = new GetProductsReply();
        response.Products.AddRange(products.Select(Map));
        return response;
    }

    /// <summary>Maps one domain product to its primitive protobuf contract.</summary>
    /// <param name="product">Catalog product.</param>
    /// <returns>Internal product snapshot.</returns>
    private static ProductReply Map(Product product) => new()
    {
        Id = product.Id.Value.ToString("D"),
        Sku = product.Sku,
        Name = product.Name,
        PriceAmount = product.Price.Amount.ToString(CultureInfo.InvariantCulture),
        PriceCurrency = product.Price.Currency,
        IsActive = product.Status == ProductStatus.Active
    };
}
