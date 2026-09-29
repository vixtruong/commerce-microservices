using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>Represents a Commerce user managed by ASP.NET Core Identity.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
}

/// <summary>Stores only a SHA-256 refresh-token hash and its rotation state.</summary>
public sealed class RefreshToken
{
    /// <summary>Gets or sets the token row identifier.</summary>
    public Guid Id { get; set; }
    /// <summary>Gets or sets the owning Identity user.</summary>
    public Guid UserId { get; set; }
    /// <summary>Gets or sets the unique hexadecimal token hash.</summary>
    public string TokenHash { get; set; } = string.Empty;
    /// <summary>Gets or sets the UTC creation time.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
    /// <summary>Gets or sets the UTC expiry time.</summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }
    /// <summary>Gets or sets the UTC revocation time.</summary>
    public DateTimeOffset? RevokedAtUtc { get; set; }
    /// <summary>Gets or sets the replacement token hash after rotation.</summary>
    public string? ReplacedByTokenHash { get; set; }
}

/// <summary>Owns users, roles, and refresh-token rotation state.</summary>
public sealed class IdentityDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    /// <summary>Initializes the Identity context.</summary>
    /// <param name="options">PostgreSQL context options.</param>
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    /// <summary>Gets hashed refresh tokens.</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>Configures the Identity schema and refresh token indexes.</summary>
    /// <param name="builder">Model builder.</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("identity");
        base.OnModelCreating(builder);
        builder.Entity<RefreshToken>(token =>
        {
            token.ToTable("refresh_tokens");
            token.HasKey(value => value.Id);
            token.Property(value => value.TokenHash).HasMaxLength(64);
            token.Property(value => value.ReplacedByTokenHash).HasMaxLength(64);
            token.HasIndex(value => value.TokenHash).IsUnique();
            token.HasIndex(value => new { value.UserId, value.ExpiresAtUtc });
        });
    }
}
