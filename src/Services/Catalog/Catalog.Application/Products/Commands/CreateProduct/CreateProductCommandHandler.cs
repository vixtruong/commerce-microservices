using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using Catalog.Application.Categories;
using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Domain.Results;
using MediatR;

namespace Catalog.Application.Products.Commands.CreateProduct
{
    /// <summary>
    /// Creates a Product aggregate and commits it through the unit of work.
    /// </summary>
    public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<CreateProductResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICategoryRepository _categories;

        /// <summary>Initializes creation with Catalog-owned category validation.</summary>
        /// <param name="productRepository">Product repository.</param>
        /// <param name="unitOfWork">Catalog transaction boundary.</param>
        /// <param name="categories">Category repository.</param>
        public CreateProductCommandHandler(
            IProductRepository productRepository,
            IUnitOfWork unitOfWork, ICategoryRepository categories)
        {
            _productRepository = productRepository;
            _unitOfWork = unitOfWork;
            _categories = categories;
        }

        /// <summary>
        /// Creates and persists a new Product aggregate.
        /// </summary>
        /// <param name="request">Product creation command.</param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>The created product identifier or a validation error.</returns>
        public async Task<Result<CreateProductResponse>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            string normalizedSku = request.Sku.Trim().ToUpperInvariant();

            bool skuExists = await _productRepository.ExistsBySkuAsync(normalizedSku, cancellationToken);

            if (skuExists)
            {
                return Result<CreateProductResponse>.Failure(
                    ProductErrors.SkuAlreadyExists(normalizedSku));
            }

            Result<Product> productResult = Product.Create(
                normalizedSku,
                request.Name,
                request.Description,
                request.PriceAmount,
                request.PriceCurrency,
                DateTimeOffset.UtcNow);

            if (productResult.IsFailure)
            {
                return productResult.Error;
            }

            Product product = productResult.Value;

            Result presentation = product.ChangePresentation(request.Brand, request.ImageUrl, request.SourceUrl,
                request.CategorySlug, DateTimeOffset.UtcNow, request.ImageUrls);
            if (presentation.IsFailure) return presentation.Error;
            if (product.CategorySlug is not null &&
                (await _categories.GetBySlugAsync(product.CategorySlug, cancellationToken))?.IsActive != true)
                return Error.Validation("Catalog.CategoryUnavailable", "Choose an existing active product group.");

            _productRepository.Add(product);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CreateProductResponse(product.Id.Value);
        }
    }
}
