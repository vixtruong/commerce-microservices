using Catalog.Domain.Products;
using Catalog.Domain.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

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

        builder.Property(product => product.Brand).HasMaxLength(80);
        builder.Property(product => product.ImageUrl).HasMaxLength(180);
        // Preserve gallery order and compare values, rather than collection references, during EF tracking.
        builder.Property(product => product.ImageUrls).HasColumnType("jsonb").HasConversion(
            images => JsonSerializer.Serialize(images, (JsonSerializerOptions?)null),
            json => (IReadOnlyList<string>)(JsonSerializer.Deserialize<string[]>(json, (JsonSerializerOptions?)null) ?? Array.Empty<string>()))
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<string>>(
                (left, right) => left != null && right != null && left.SequenceEqual(right),
                images => images.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                images => images.ToArray()));
        builder.Property(product => product.SourceUrl).HasMaxLength(1000);
        builder.Property(product => product.CategorySlug).HasMaxLength(120);
        // A category belongs to this database; never delete a group underneath existing product assignments.
        builder.HasOne<Category>().WithMany().HasForeignKey(product => product.CategorySlug)
            .HasPrincipalKey(category => category.Slug).OnDelete(DeleteBehavior.Restrict);

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
