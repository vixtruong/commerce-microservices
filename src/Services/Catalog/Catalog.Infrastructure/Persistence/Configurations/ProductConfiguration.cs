using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures how the Product aggregate is stored in PostgreSQL.
/// </summary>
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <summary>
    /// Configures the product table, constraints, value object, and indexes.
    /// </summary>
    /// <param name="builder">The Product entity type builder.</param>
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Id)
            .HasConversion(
                productId => productId.Value,
                value => ProductId.From(value))
            .ValueGeneratedNever();

        builder.Property(product => product.Sku)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(product => product.Sku)
            .IsUnique();

        builder.Property(product => product.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(product => product.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(product => product.Status)
            // Persist the enum name so database values remain readable and do not depend on numeric ordering.
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(product => product.CreatedAtUtc)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(product => product.UpdatedAtUtc)
            .HasPrecision(0)
            .IsRequired();

        builder.OwnsOne(product => product.Price, priceBuilder =>
        {
            priceBuilder.Property(price => price.Amount)
                .HasColumnName("price_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            priceBuilder.Property(price => price.Currency)
                .HasColumnName("price_currency")
                .HasColumnType("char(3)")
                .IsRequired();
        });

        builder.Navigation(product => product.Price)
            .IsRequired();

        // Domain events are transient application state and must never become relational data.
        builder.Ignore(product => product.DomainEvents);
    }
}
