using Catalog.Domain.Products;

namespace Catalog.Application.Abstractions
{
    /// <summary>
    /// Defines Product aggregate persistence operations.
    /// </summary>
    public interface IProductRepository
    {
        /// <summary>
        /// Adds a new Product aggregate to the current unit of work.
        /// </summary>
        /// <param name="product">Product aggregate to add.</param>
        void Add(Product product);

        /// <summary>
        /// Gets a product for read-only access.
        /// </summary>
        /// <param name="productId">Product identifier.</param>
        /// <param name="cancellationToken">
        /// Token used to cancel the database operation.
        /// </param>
        /// <returns>The product when found; otherwise null.</returns>
        Task<Product?> GetByIdAsync(
            ProductId productId,
            CancellationToken cancellationToken);

        /// <summary>Gets a tracked product for a command mutation.</summary>
        /// <param name="productId">Product identifier.</param>
        /// <param name="cancellationToken">Token used to cancel database I/O.</param>
        /// <returns>The tracked product, or null.</returns>
        Task<Product?> GetForUpdateAsync(ProductId productId, CancellationToken cancellationToken);

        /// <summary>Gets a read-only, filtered page of products.</summary>
        /// <param name="search">Optional SKU, name, or description fragment.</param>
        /// <param name="skip">Number of rows to skip.</param>
        /// <param name="take">Bounded page size.</param>
        /// <param name="cancellationToken">Token used to cancel database I/O.</param>
        /// <returns>The matching product page and total count.</returns>
        Task<(IReadOnlyCollection<Product> Products, int Total)> ListAsync(
            string? search,
            int skip,
            int take,
            CancellationToken cancellationToken);

        /// <summary>Gets product snapshots for internal batch checkout validation.</summary>
        /// <param name="productIds">Product identifiers.</param>
        /// <param name="cancellationToken">Token used to cancel database I/O.</param>
        /// <returns>Matching products.</returns>
        Task<IReadOnlyCollection<Product>> GetByIdsAsync(
            IReadOnlyCollection<ProductId> productIds,
            CancellationToken cancellationToken);

        /// <summary>
        /// Determines whether a normalized SKU already exists.
        /// </summary>
        /// <param name="sku">Normalized product SKU.</param>
        /// <param name="cancellationToken">
        /// Token used to cancel the database operation.
        /// </param>
        /// <returns>True when the SKU exists.</returns>
        Task<bool> ExistsBySkuAsync(
            string sku,
            CancellationToken cancellationToken);

        Task<bool> ExistsByIdAsync(
            ProductId productId,
            CancellationToken cancellationToken);
    }

    /// <summary>Provides cache-aside storage for serialized Catalog response DTOs.</summary>
    public interface IProductCache
    {
        /// <summary>Gets a cached product response.</summary>
        /// <param name="productId">Product identifier.</param>
        /// <param name="cancellationToken">Token used to cancel Redis I/O.</param>
        /// <returns>The cached response, or null on miss.</returns>
        Task<Products.Queries.GetProductById.ProductResponse?> GetAsync(Guid productId, CancellationToken cancellationToken);

        /// <summary>Stores a product response with a bounded TTL.</summary>
        /// <param name="response">Serialized-safe product response.</param>
        /// <param name="cancellationToken">Token used to cancel Redis I/O.</param>
        /// <returns>A task that completes after Redis accepts the value.</returns>
        Task SetAsync(Products.Queries.GetProductById.ProductResponse response, CancellationToken cancellationToken);

        /// <summary>Invalidates a product after mutation.</summary>
        /// <param name="productId">Product identifier.</param>
        /// <param name="cancellationToken">Token used to cancel Redis I/O.</param>
        /// <returns>A task that completes after removal.</returns>
        Task RemoveAsync(Guid productId, CancellationToken cancellationToken);
    }
}
