using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Commerce.BuildingBlocks.Application.Security;
using Commerce.BuildingBlocks.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Architecture.Tests;

/// <summary>Verifies standard HTTP permission enforcement and loaded-resource ownership.</summary>
public sealed class PermissionAuthorizationTests
{
    /// <summary>Verifies unauthenticated, insufficient-permission, and permitted HTTP requests.</summary>
    /// <param name="authenticated">Whether a principal is present.</param>
    /// <param name="permission">Whether the required permission is present.</param>
    /// <param name="expected">Expected HTTP status.</param>
    /// <returns>A task representing HTTP policy validation.</returns>
    [Theory]
    [InlineData(false, false, HttpStatusCode.Unauthorized)]
    [InlineData(true, false, HttpStatusCode.Forbidden)]
    [InlineData(true, true, HttpStatusCode.OK)]
    public async Task PermissionPolicy_RequestAuthority_EnforcesExpectedStatusAsync(bool authenticated, bool permission, HttpStatusCode expected)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        await using WebApplication application = builder.Build();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapGet("/protected", () => Results.Ok()).RequireAuthorization(Permissions.InventoryAdjust);
        await application.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(application.Urls.Single()) };
        if (authenticated) client.DefaultRequestHeaders.Add("X-Test-User", "present");
        if (permission) client.DefaultRequestHeaders.Add("X-Test-Permission", Permissions.InventoryAdjust);
        using HttpResponseMessage response = await client.GetAsync("/protected");
        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>Verifies owner, unrelated customer, and privileged principal authorization.</summary>
    /// <returns>A task representing resource authorization validation.</returns>
    [Fact]
    public async Task OrderResource_OwnerOrElevatedPermission_RejectsUnrelatedCustomerAsync()
    {
        Guid ownerId = Guid.NewGuid();
        var requirement = new OrderResourceRequirement();
        var handler = new OrderResourceAuthorizationHandler();
        foreach ((Guid subject, bool elevated, bool allowed) in new[] { (ownerId, false, true), (Guid.NewGuid(), false, false), (Guid.NewGuid(), true, true) })
        {
            var claims = new List<Claim> { new("sub", subject.ToString()) };
            if (elevated) claims.Add(new(Permissions.ClaimType, Permissions.OrderRead));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            var context = new AuthorizationHandlerContext([requirement], principal, new OwnedResource(ownerId));
            await handler.HandleAsync(context);
            Assert.Equal(allowed, context.HasSucceeded);
        }
    }

    /// <summary>Provides deterministic authenticated principals solely within HTTP policy tests.</summary>
    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        /// <summary>Initializes the test-only scheme.</summary>
        /// <param name="options">Scheme options.</param>
        /// <param name="logger">Test logger.</param>
        /// <param name="encoder">URL encoder.</param>
        public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        /// <summary>Builds a test principal from deliberately local test headers.</summary>
        /// <returns>An authenticated ticket or no result.</returns>
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("X-Test-User")) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()) };
            if (Request.Headers.TryGetValue("X-Test-Permission", out var permission))
                claims.Add(new(Permissions.ClaimType, permission.ToString()));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
}
