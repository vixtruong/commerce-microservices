using Identity.Application.Authentication;
using Identity.Infrastructure.Authentication;
using Identity.Infrastructure.Persistence;
using Commerce.BuildingBlocks.Infrastructure.Health;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure;

/// <summary>Registers ASP.NET Core Identity, PostgreSQL, and secure token services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds Identity infrastructure.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Service configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string database = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("Connection string 'IdentityDb' was not configured.");
        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(database, npgsql => npgsql.EnableRetryOnFailure(3)));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.User.RequireUniqueEmail = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddDefaultTokenProviders();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserAdministration, UserAdministration>();
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddScoped<IAccessManagement, AccessManagement>();
        services.AddPostgresReadiness<IdentityDbContext>();
        return services;
    }
}
