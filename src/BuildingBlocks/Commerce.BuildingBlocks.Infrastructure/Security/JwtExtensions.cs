using System.Text;
using Commerce.BuildingBlocks.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Commerce.BuildingBlocks.Infrastructure.Security;

/// <summary>Configures consistent JWT validation at the gateway and every protected service boundary.</summary>
public static class JwtExtensions
{
    /// <summary>Adds issuer, audience, lifetime, and signature validation.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration containing the non-committed signing key.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required JWT configuration is missing.</exception>
    public static IServiceCollection AddCommerceJwt(this IServiceCollection services, IConfiguration configuration)
    {
        string issuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer must be configured.");
        string audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience must be configured.");
        string signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey must be supplied through environment variables or user secrets.");

        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "sub",
                RoleClaimType = "role"
            };
        });
        services.AddAuthorization(options => options.AddPolicy(Permissions.OrderResourcePolicy,
            policy => policy.RequireAuthenticatedUser().AddRequirements(new OrderResourceRequirement())));
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, OrderResourceAuthorizationHandler>();
        return services;
    }
}
