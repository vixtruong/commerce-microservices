using Commerce.BuildingBlocks.Domain.Entities;
using Commerce.BuildingBlocks.Domain.Results;

namespace Catalog.Domain.Categories;

/// <summary>Identifies a Catalog category.</summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct CategoryId(Guid Value)
{
    /// <summary>Creates a new category identifier.</summary>
    /// <returns>A unique identifier.</returns>
    public static CategoryId New() => new(Guid.NewGuid());
}

/// <summary>Defines Category aggregate validation failures.</summary>
public static class CategoryErrors
{
    /// <summary>Returned when a category name is empty.</summary>
    public static readonly Error NameRequired = Error.Validation("Catalog.CategoryNameRequired", "Category name is required.");
}

/// <summary>Groups products under a stable normalized slug owned by Catalog.</summary>
public sealed class Category : AggregateRoot<CategoryId>
{
    private Category()
    {
    }

    private Category(CategoryId id, string name, string slug, DateTimeOffset createdAtUtc) : base(id)
    {
        Name = name;
        Slug = slug;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Gets the display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the unique normalized URL-safe name.</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>Gets whether products may be assigned to this category.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the UTC creation time.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Creates a validated category.</summary>
    /// <param name="name">Display name.</param>
    /// <param name="slug">Unique normalized slug.</param>
    /// <param name="createdAtUtc">UTC creation time.</param>
    /// <param name="categoryId">Optional stable import identifier.</param>
    /// <returns>A category or validation error.</returns>
    public static Result<Category> Create(
        string name,
        string slug,
        DateTimeOffset createdAtUtc,
        CategoryId? categoryId = null)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug))
        {
            return CategoryErrors.NameRequired;
        }

        return new Category(categoryId ?? CategoryId.New(), name.Trim(), slug.Trim().ToLowerInvariant(), createdAtUtc);
    }

    /// <summary>Activates the category for product assignment.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Deactivates the category without deleting historical assignments.</summary>
    public void Deactivate() => IsActive = false;
}
