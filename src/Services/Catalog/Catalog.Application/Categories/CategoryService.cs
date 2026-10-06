using Catalog.Domain.Categories;
using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Domain.Results;

namespace Catalog.Application.Categories;

/// <summary>Represents a Catalog-owned product group and its current published product count.</summary>
/// <param name="Id">Stable category identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Slug">Stable URL/filter key.</param>
/// <param name="IsActive">Whether new assignments are allowed.</param>
/// <param name="ProductCount">Number of active products assigned to this group.</param>
public sealed record CategoryResponse(Guid Id, string Name, string Slug, bool IsActive, int ProductCount);

/// <summary>Defines persistence owned exclusively by the Catalog service.</summary>
public interface ICategoryRepository
{
    /// <summary>Gets groups with server-computed publication counts.</summary>
    /// <param name="includeInactive">Whether inactive groups should be returned.</param>
    /// <param name="cancellationToken">Database cancellation.</param>
    /// <returns>Ordered group snapshots.</returns>
    Task<IReadOnlyCollection<CategoryResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    /// <summary>Gets a tracked category by slug for validated assignment or administration.</summary>
    /// <param name="slug">Normalized slug.</param>
    /// <param name="cancellationToken">Database cancellation.</param>
    /// <returns>The category or null.</returns>
    Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken);

    /// <summary>Stages a new group in the Catalog transaction.</summary>
    /// <param name="category">Validated category aggregate.</param>
    void Add(Category category);
}

/// <summary>Provides group discovery and authorized administrative use cases.</summary>
public interface ICategoryService
{
    /// <summary>Gets product groups and actual published counts.</summary>
    /// <param name="includeInactive">Whether inactive groups should be included.</param>
    /// <param name="cancellationToken">Database cancellation.</param>
    /// <returns>Group snapshots.</returns>
    Task<IReadOnlyCollection<CategoryResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    /// <summary>Creates a group while preserving unique slugs.</summary>
    /// <param name="name">Display name.</param>
    /// <param name="slug">Stable URL-safe slug.</param>
    /// <param name="cancellationToken">Database cancellation.</param>
    /// <returns>The created group or a validation/conflict error.</returns>
    Task<Result<CategoryResponse>> CreateAsync(string name, string slug, CancellationToken cancellationToken);

    /// <summary>Changes display name/availability while retaining product assignments.</summary>
    /// <param name="slug">Existing stable slug.</param>
    /// <param name="name">New display name.</param>
    /// <param name="isActive">Whether new assignments are allowed.</param>
    /// <param name="cancellationToken">Database cancellation.</param>
    /// <returns>Success or a validation/not-found error.</returns>
    Task<Result> UpdateAsync(string slug, string name, bool isActive, CancellationToken cancellationToken);
}

/// <summary>Protects category identity and leaves historical product assignments intact.</summary>
public sealed class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initializes category use cases.</summary>
    /// <param name="repository">Catalog category repository.</param>
    /// <param name="unitOfWork">Catalog transaction boundary.</param>
    public CategoryService(ICategoryRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<CategoryResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        _repository.ListAsync(includeInactive, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<CategoryResponse>> CreateAsync(string name, string slug, CancellationToken cancellationToken)
    {
        Result<Category> result = Category.Create(name, slug, DateTimeOffset.UtcNow);
        if (result.IsFailure) return result.Error;
        Category category = result.Value;
        if (await _repository.GetBySlugAsync(category.Slug, cancellationToken) is not null)
            return Error.Conflict("Catalog.CategorySlugExists", "This category slug already exists.");
        _repository.Add(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new CategoryResponse(category.Id.Value, category.Name, category.Slug, category.IsActive, 0);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(string slug, string name, bool isActive, CancellationToken cancellationToken)
    {
        Category? category = await _repository.GetBySlugAsync(slug.Trim().ToLowerInvariant(), cancellationToken);
        if (category is null) return Error.NotFound("Catalog.CategoryNotFound", "The category does not exist.");
        // Disabling a group stops new assignments; it does not unpublish or erase its existing products.
        Result result = category.Update(name, isActive);
        if (result.IsFailure) return result;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
