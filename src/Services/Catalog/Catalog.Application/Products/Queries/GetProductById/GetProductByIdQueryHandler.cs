using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using Commerce.BuildingBlocks.Domain.Results;
using MediatR;

namespace Catalog.Application.Products.Queries.GetProductById
{
    /// <summary>Implements a cache-aside product lookup.</summary>
    public sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Result<ProductResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IProductCache _productCache;

        /// <summary>Initializes the query handler.</summary>
        /// <param name="productRepository">Authoritative Catalog repository.</param>
        /// <param name="productCache">Redis response cache.</param>
        public GetProductByIdQueryHandler(IProductRepository productRepository, IProductCache productCache)
        {
            _productRepository = productRepository;
            _productCache = productCache;
        }

        /// <summary>
        /// Gets one product by identifier.
        /// </summary>
        /// <param name="request">Product query.</param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>The product response or a not-found error.</returns>
        public async Task<Result<ProductResponse>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
        {
            if (request.ProductId == Guid.Empty)
            {
                return Result<ProductResponse>.Failure(ProductErrors.IdRequired);
            }

            ProductId productId = ProductId.From(request.ProductId);

            ProductResponse? cached = await _productCache.GetAsync(request.ProductId, cancellationToken);
            if (cached is not null)
            {
                return cached;
            }

            Product? product = await _productRepository.GetByIdAsync(productId, cancellationToken);

            if (product is null)
            {
                return Result<ProductResponse>.Failure(ProductErrors.NotFound(productId));
            }

            ProductResponse response = new(
                product.Id.Value,
                product.Sku,
                product.Name,
                product.Description,
                product.Price.Amount,
                product.Price.Currency,
                product.Status.ToString(),
                product.CreatedAtUtc,
                product.UpdatedAtUtc, product.Brand, product.ImageUrl, product.SourceUrl, product.CategorySlug,
                product.ImageUrls.Count > 0 ? product.ImageUrls : product.ImageUrl is null ? [] : [product.ImageUrl]);

            await _productCache.SetAsync(response, cancellationToken);

            return response;
        }
    }
}
