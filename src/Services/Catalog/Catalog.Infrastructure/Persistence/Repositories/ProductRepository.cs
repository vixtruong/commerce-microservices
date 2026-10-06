using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence.Repositories
{
    /// <summary>Provides Catalog-owned EF Core product persistence.</summary>
    public sealed class ProductRepository : IProductRepository
    {
        private readonly CatalogDbContext _dbContext;

        /// <summary>Initializes the repository.</summary>
        /// <param name="dbContext">Catalog database context.</param>
        public ProductRepository(CatalogDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <inheritdoc />
        public void Add(Product product)
        {
            _dbContext.Products.Add(product);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByIdAsync(ProductId productId, CancellationToken cancellationToken)
        {
            return await _dbContext.Products
                .AsNoTracking()
                .AnyAsync(p => p.Id == productId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken)
        {
            return await _dbContext.Products
                .AsNoTracking()
                .AnyAsync(p => p.Sku == sku, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Product?> GetByIdAsync(ProductId productId, CancellationToken cancellationToken)
        {
            return await _dbContext.Products
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.Id == productId, cancellationToken);
        }

        /// <inheritdoc />
        public Task<Product?> GetForUpdateAsync(ProductId productId, CancellationToken cancellationToken) =>
            _dbContext.Products.SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);

        /// <inheritdoc />
        public async Task<(IReadOnlyCollection<Product> Products, int Total)> ListAsync(
            string? search,
            int skip,
            int take,
            CancellationToken cancellationToken, string? status = null, string sort = "name",
            decimal? minPrice = null, decimal? maxPrice = null)
        {
            IQueryable<Product> query = _dbContext.Products.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                // PostgreSQL ILIKE provides case-insensitive search without loading the result set.
                query = query.Where(product =>
                    EF.Functions.ILike(product.Sku, $"%{term}%") ||
                    EF.Functions.ILike(product.Name, $"%{term}%") ||
                    EF.Functions.ILike(product.Description, $"%{term}%"));
            }

            if (status is not null && Enum.TryParse(status, out ProductStatus publication)) query = query.Where(p => p.Status == publication);
            if (minPrice.HasValue) query = query.Where(p => p.Price.Amount >= minPrice.Value);
            if (maxPrice.HasValue) query = query.Where(p => p.Price.Amount <= maxPrice.Value);
            int total = await query.CountAsync(cancellationToken);
            IOrderedQueryable<Product> ordered = sort switch
            {
                "price-asc" => query.OrderBy(p => p.Price.Amount),
                "price-desc" => query.OrderByDescending(p => p.Price.Amount),
                "newest" => query.OrderByDescending(p => p.CreatedAtUtc),
                _ => query.OrderBy(p => p.Name)
            };
            // Stable ties prevent duplicates or gaps when moving between adjacent pages.
            Product[] products = await ordered.ThenBy(p => p.Id).Skip(skip).Take(take).ToArrayAsync(cancellationToken);
            return (products, total);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<Product>> GetByIdsAsync(
            IReadOnlyCollection<ProductId> productIds,
            CancellationToken cancellationToken) =>
            await _dbContext.Products.AsNoTracking()
                .Where(product => productIds.Contains(product.Id))
                .ToArrayAsync(cancellationToken);
    }
}
