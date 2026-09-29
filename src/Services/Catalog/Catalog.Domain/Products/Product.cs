using Catalog.Domain.Common;
using Catalog.Domain.Products.Events;
using Commerce.BuildingBlocks.Domain.Entities;
using Commerce.BuildingBlocks.Domain.Results;

namespace Catalog.Domain.Products
{
    /// <summary>
    /// Represents a Catalog product and protects its pricing and publication lifecycle.
    /// </summary>
    public class Product : AggregateRoot<ProductId>
    {
        private Product(
            ProductId id,
            string sku,
            string name,
            string description,
            Money price,
            DateTimeOffset createdAtUtc)
            : base(id)
        {
            Sku = sku;
            Name = name;
            Description = description;
            Price = price;
            Status = ProductStatus.Draft;
            CreatedAtUtc = createdAtUtc;
        }

        /// <summary>Initializes an empty product for Entity Framework Core.</summary>
        private Product() { }

        /// <summary>Gets the normalized, unique stock-keeping unit.</summary>
        public string Sku { get; private set; } = string.Empty;

        /// <summary>Gets the customer-facing product name.</summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>Gets the customer-facing product description.</summary>
        public string Description { get; private set; } = string.Empty;

        /// <summary>Gets the current authoritative Catalog price.</summary>
        public Money Price { get; private set; } = null!;

        /// <summary>Gets the publication state.</summary>
        public ProductStatus Status { get; private set; }

        /// <summary>Gets the UTC creation time.</summary>
        public DateTimeOffset CreatedAtUtc { get; private set; }

        /// <summary>Gets the UTC time of the latest change.</summary>
        public DateTimeOffset UpdatedAtUtc { get; private set; }

        /// <summary>Creates a validated Catalog product.</summary>
        /// <param name="sku">Unique stock-keeping unit.</param>
        /// <param name="name">Customer-facing name.</param>
        /// <param name="description">Optional customer-facing description.</param>
        /// <param name="priceAmount">Non-negative monetary amount.</param>
        /// <param name="priceCurrency">Three-letter ISO currency code.</param>
        /// <param name="createdAtUtc">UTC creation time.</param>
        /// <param name="productId">Optional stable identifier used by deterministic imports and seed data.</param>
        /// <returns>The new product or a validation error.</returns>
        public static Result<Product> Create(
            string sku,
            string name,
            string? description,
            decimal priceAmount,
            string priceCurrency,
            DateTimeOffset createdAtUtc,
            ProductId? productId = null)
        {
            if (string.IsNullOrWhiteSpace(sku))
            {
                return ProductErrors.SkuRequired;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return ProductErrors.NameRequired;
            }

            Result<Money> priceResult = Money.Create(priceAmount, priceCurrency);

            if (priceResult.IsFailure)
            {
                return priceResult.Error;
            }

            var product = new Product(
                productId ?? ProductId.New(),
                sku.Trim().ToUpperInvariant(),
                name.Trim(),
                description?.Trim() ?? string.Empty,
                priceResult.Value,
                createdAtUtc);

            product.RaiseDomainEvent(
                new ProductCreatedDomainEvent(
                    product.Id.Value,
                    product.Sku,
                    product.Name));

            return product;
        }

        /// <summary>Changes the product name.</summary>
        /// <param name="name">New non-empty name.</param>
        /// <param name="changedAtUtc">UTC change time.</param>
        /// <returns>A success or validation error.</returns>
        public Result Rename(string name, DateTimeOffset changedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return ProductErrors.NameRequired;
            }

            Name = name.Trim();
            UpdatedAtUtc = changedAtUtc;
            return Result.Success();
        }

        /// <summary>Changes the optional product description.</summary>
        /// <param name="description">New description.</param>
        /// <param name="changedAtUtc">UTC change time.</param>
        /// <returns>A successful result.</returns>
        public Result ChangeDescription(string? description, DateTimeOffset changedAtUtc)
        {
            Description = description?.Trim() ?? string.Empty;
            UpdatedAtUtc = changedAtUtc;
            return Result.Success();
        }

        /// <summary>Changes the authoritative Catalog price.</summary>
        /// <param name="amount">New non-negative amount.</param>
        /// <param name="currency">Three-letter ISO currency code.</param>
        /// <param name="changedAtUtc">UTC change time.</param>
        /// <returns>A success or validation error.</returns>
        public Result ChangePrice(
            decimal amount,
            string currency,
            DateTimeOffset changedAtUtc)
        {
            Result<Money> priceResult = Money.Create(amount, currency);

            if (priceResult.IsFailure)
            {
                return priceResult.Error;
            }

            Money newPrice = priceResult.Value;

            if (Price.Equals(newPrice))
            {
                return Result.Success();
            }

            decimal previousAmount = Price.Amount;
            Price = newPrice;
            UpdatedAtUtc = changedAtUtc;

            RaiseDomainEvent(
                new ProductPriceChangedDomainEvent(
                    Id.Value,
                    previousAmount,
                    Price.Amount,
                    Price.Currency));

            return Result.Success();
        }

        /// <summary>Makes the product available to clients and checkout.</summary>
        /// <param name="changedAtUtc">UTC activation time.</param>
        /// <returns>A successful result.</returns>
        public Result Activate(DateTimeOffset changedAtUtc)
        {
            if (Status == ProductStatus.Active)
            {
                return Result.Success();
            }

            Status = ProductStatus.Active;
            UpdatedAtUtc = changedAtUtc;
            return Result.Success();
        }

        /// <summary>Removes the product from sale without deleting history.</summary>
        /// <param name="changedAtUtc">UTC deactivation time.</param>
        /// <returns>A successful result.</returns>
        public Result Deactivate(DateTimeOffset changedAtUtc)
        {
            if (Status == ProductStatus.Inactive)
            {
                return Result.Success();
            }

            Status = ProductStatus.Inactive;
            UpdatedAtUtc = changedAtUtc;
            return Result.Success();
        }
    }
}
