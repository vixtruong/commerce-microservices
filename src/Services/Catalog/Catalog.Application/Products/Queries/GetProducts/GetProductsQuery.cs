using Catalog.Application.Abstractions;
using Catalog.Application.Products.Queries.GetProductById;
using MediatR;

namespace Catalog.Application.Products.Queries.GetProducts;

/// <summary>Requests a bounded, searchable product page.</summary>
/// <param name="Search">Optional SKU, name, or description fragment.</param>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Requested bounded page size.</param>
public sealed record GetProductsQuery(string? Search, int Page = 1, int PageSize = 20) : IRequest<ProductPageResponse>;

/// <summary>Represents a paged Catalog response.</summary>
/// <param name="Items">Matching products.</param>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Applied page size.</param>
/// <param name="Total">Total matching rows.</param>
public sealed record ProductPageResponse(
    IReadOnlyCollection<ProductResponse> Items,
    int Page,
    int PageSize,
    int Total);

/// <summary>Executes read-only product search and projection.</summary>
public sealed class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, ProductPageResponse>
{
    private readonly IProductRepository _repository;

    /// <summary>Initializes the handler.</summary>
    /// <param name="repository">Catalog repository.</param>
    public GetProductsQueryHandler(IProductRepository repository) => _repository = repository;

    /// <summary>Returns one normalized product page.</summary>
    /// <param name="request">Search request.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The product page.</returns>
    public async Task<ProductPageResponse> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);
        var (products, total) = await _repository.ListAsync(request.Search, (page - 1) * pageSize, pageSize, cancellationToken);
        ProductResponse[] responses = products.Select(ProductMappings.ToResponse).ToArray();
        return new ProductPageResponse(responses, page, pageSize, total);
    }
}

/// <summary>Maps domain products into stable application response DTOs.</summary>
public static class ProductMappings
{
    /// <summary>Projects one product without leaking a tracked entity.</summary>
    /// <param name="product">Domain product.</param>
    /// <returns>Serialized-safe response.</returns>
    public static ProductResponse ToResponse(Domain.Products.Product product) => new(
        product.Id.Value,
        product.Sku,
        product.Name,
        product.Description,
        product.Price.Amount,
        product.Price.Currency,
        product.Status.ToString(),
        product.CreatedAtUtc,
        product.UpdatedAtUtc);
}
