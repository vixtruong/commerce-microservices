using Catalog.Domain.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Infrastructure.Persistence.Configurations;

/// <summary>Configures Catalog category persistence and slug uniqueness.</summary>
internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id)
            .HasConversion(id => id.Value, value => new CategoryId(value))
            .ValueGeneratedNever();
        builder.Property(category => category.Name).HasMaxLength(120).IsRequired();
        builder.Property(category => category.Slug).HasMaxLength(120).IsRequired();
        builder.HasIndex(category => category.Slug).IsUnique();
        builder.Ignore(category => category.DomainEvents);
    }
}
