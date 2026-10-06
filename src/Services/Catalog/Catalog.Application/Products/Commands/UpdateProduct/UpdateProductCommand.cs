using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using Catalog.Application.Categories;
using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Domain.Results;
using MediatR;

namespace Catalog.Application.Products.Commands.UpdateProduct;

/// <summary>Updates mutable product details while retaining SKU identity.</summary>
/// <param name="ProductId">Product identifier.</param>
/// <param name="Name">New display name.</param>
/// <param name="Description">New description.</param>
/// <param name="PriceAmount">New price amount.</param>
/// <param name="PriceCurrency">New price currency.</param>
/// <param name="Brand">Manufacturer; null preserves and empty clears.</param>
/// <param name="ImageUrl">Catalog image path; null preserves and empty clears.</param>
/// <param name="SourceUrl">Manufacturer reference; null preserves and empty clears.</param>
/// <param name="CategorySlug">Group; null preserves and empty clears.</param>
/// <param name="ImageUrls">Ordered gallery; null preserves and empty clears.</param>
public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string? Description,
    decimal PriceAmount,
    string PriceCurrency, string? Brand = null, string? ImageUrl = null,
    string? SourceUrl = null, string? CategorySlug = null, IReadOnlyCollection<string>? ImageUrls = null) : IRequest<Result>;

/// <summary>Activates or deactivates a product explicitly.</summary>
/// <param name="ProductId">Product identifier.</param>
/// <param name="Activate">True to activate; false to deactivate.</param>
public sealed record SetProductActivationCommand(Guid ProductId, bool Activate) : IRequest<Result>;

/// <summary>Handles product content and price changes.</summary>
public sealed class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result>
{
    private readonly IProductRepository _repository;
    private readonly IProductCache _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryRepository _categories;

    /// <summary>Initializes the update handler.</summary>
    /// <param name="repository">Catalog repository.</param>
    /// <param name="cache">Redis cache invalidator.</param>
    /// <param name="unitOfWork">Catalog transaction boundary.</param>
    /// <param name="categories">Catalog category repository.</param>
    public UpdateProductCommandHandler(IProductRepository repository, IProductCache cache, IUnitOfWork unitOfWork,
        ICategoryRepository categories)
    {
        _repository = repository;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _categories = categories;
    }

    /// <summary>Updates a product and invalidates its cached DTO after commit.</summary>
    /// <param name="request">Update command.</param>
    /// <param name="cancellationToken">Token used to cancel I/O.</param>
    /// <returns>A success or domain error.</returns>
    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        ProductId id = ProductId.From(request.ProductId);
        Product? product = await _repository.GetForUpdateAsync(id, cancellationToken);
        if (product is null)
        {
            return ProductErrors.NotFound(id);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string? categorySlug = request.CategorySlug is null ? product.CategorySlug :
            string.IsNullOrWhiteSpace(request.CategorySlug) ? null : request.CategorySlug.Trim().ToLowerInvariant();
        // Disabled groups retain old assignments; new assignments must target an active group.
        if (categorySlug is not null && categorySlug != product.CategorySlug &&
            (await _categories.GetBySlugAsync(categorySlug, cancellationToken))?.IsActive != true)
            return Error.Validation("Catalog.CategoryUnavailable", "Choose an existing active product group.");
        Result result = product.Rename(request.Name, now);
        if (result.IsSuccess) result = product.ChangeDescription(request.Description, now);
        if (result.IsSuccess) result = product.ChangePrice(request.PriceAmount, request.PriceCurrency, now);
        if (result.IsSuccess) result = product.ChangePresentation(request.Brand ?? product.Brand,
            request.ImageUrl ?? product.ImageUrl, request.SourceUrl ?? product.SourceUrl, categorySlug, now, request.ImageUrls);
        if (result.IsFailure) return result;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(request.ProductId, cancellationToken);
        return Result.Success();
    }
}

/// <summary>Handles guarded product activation transitions.</summary>
public sealed class SetProductActivationCommandHandler : IRequestHandler<SetProductActivationCommand, Result>
{
    private readonly IProductRepository _repository;
    private readonly IProductCache _cache;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initializes the activation handler.</summary>
    /// <param name="repository">Catalog repository.</param>
    /// <param name="cache">Redis cache invalidator.</param>
    /// <param name="unitOfWork">Catalog transaction boundary.</param>
    public SetProductActivationCommandHandler(IProductRepository repository, IProductCache cache, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _cache = cache;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Applies activation and invalidates the response cache.</summary>
    /// <param name="request">Activation command.</param>
    /// <param name="cancellationToken">Token used to cancel I/O.</param>
    /// <returns>A success or not-found result.</returns>
    public async Task<Result> Handle(SetProductActivationCommand request, CancellationToken cancellationToken)
    {
        ProductId id = ProductId.From(request.ProductId);
        Product? product = await _repository.GetForUpdateAsync(id, cancellationToken);
        if (product is null) return ProductErrors.NotFound(id);
        Result result = request.Activate ? product.Activate(DateTimeOffset.UtcNow) : product.Deactivate(DateTimeOffset.UtcNow);
        if (result.IsFailure) return result;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(request.ProductId, cancellationToken);
        return Result.Success();
    }
}
