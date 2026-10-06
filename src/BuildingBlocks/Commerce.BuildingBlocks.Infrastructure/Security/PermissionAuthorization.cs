using System.Security.Claims;
using Commerce.BuildingBlocks.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Commerce.BuildingBlocks.Infrastructure.Security;

/// <summary>Requires one permission from validated JWT claims.</summary>
/// <param name="Permission">Cataloged permission.</param>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>Builds cataloged policies dynamically while preserving standard ASP.NET policies.</summary>
public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    /// <summary>Initializes the policy provider.</summary>
    /// <param name="options">Standard authorization options.</param>
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

    /// <summary>Resolves a supported permission or delegates to the standard provider.</summary>
    /// <param name="policyName">Requested policy.</param>
    /// <returns>Authorization policy or null.</returns>
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) =>
        Permissions.All.Contains(policyName)
            ? Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName)).Build())
            : base.GetPolicyAsync(policyName);
}

/// <summary>Checks effective permission claims signed by Identity.</summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>Satisfies a permission only when the validated principal contains it.</summary>
    /// <param name="context">Authorization evaluation.</param>
    /// <param name="requirement">Required permission.</param>
    /// <returns>A completed evaluation task.</returns>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.User.HasClaim(Permissions.ClaimType, requirement.Permission))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>Requires Order ownership or elevated read permission.</summary>
public sealed class OrderResourceRequirement : IAuthorizationRequirement;

/// <summary>Authorizes a loaded resource against its trusted owner.</summary>
public sealed class OrderResourceAuthorizationHandler : AuthorizationHandler<OrderResourceRequirement, OwnedResource>
{
    /// <summary>Allows the owner or a principal with cross-customer Order read permission.</summary>
    /// <param name="context">Authorization evaluation.</param>
    /// <param name="requirement">Resource requirement.</param>
    /// <param name="resource">Owner loaded from persisted Order state.</param>
    /// <returns>A completed evaluation task.</returns>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OrderResourceRequirement requirement, OwnedResource resource)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            (Guid.TryParse(context.User.FindFirstValue("sub"), out Guid subject) && subject == resource.OwnerId ||
                context.User.HasClaim(Permissions.ClaimType, Permissions.OrderRead)))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
