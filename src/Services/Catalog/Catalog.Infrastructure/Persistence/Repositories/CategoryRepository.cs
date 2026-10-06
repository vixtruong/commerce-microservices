using Catalog.Application.Categories;
using Catalog.Domain.Categories;
using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence.Repositories;

/// <summary>Stores Catalog categories and computes product counts without crossing service databases.</summary>
public sealed class CategoryRepository : ICategoryRepository
{
    private readonly CatalogDbContext _context;

    /// <summary>Initializes the category repository.</summary>
    /// <param name="context">Catalog-owned database.</param>
    public CategoryRepository(CatalogDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<CategoryResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        await _context.Categories.AsNoTracking().Where(category => includeInactive || category.IsActive)
            .OrderBy(category => category.Name)
            // Count publication in SQL; the UI must not invent collection sizes or load every product.
            .Select(category => new CategoryResponse(category.Id.Value, category.Name, category.Slug, category.IsActive,
                _context.Products.Count(product => product.CategorySlug == category.Slug && product.Status == ProductStatus.Active)))
            .ToArrayAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
        _context.Categories.SingleOrDefaultAsync(category => category.Slug == slug, cancellationToken);

    /// <inheritdoc />
    public void Add(Category category) => _context.Categories.Add(category);
}
