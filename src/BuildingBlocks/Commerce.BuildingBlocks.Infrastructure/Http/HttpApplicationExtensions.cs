using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;

namespace Commerce.BuildingBlocks.Infrastructure.Http;

/// <summary>Provides consistent edge behavior and health endpoints.</summary>
public static class HttpApplicationExtensions
{
    /// <summary>Adds problem details and base health services.</summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCommerceHttpDefaults(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddHealthChecks().AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), ["live"]);
        return services;
    }

    /// <summary>Adds correlation and safe exception responses.</summary>
    /// <param name="app">Application pipeline.</param>
    /// <returns>The application.</returns>
    public static WebApplication UseCommerceHttpDefaults(this WebApplication app)
    {
        app.UseMiddleware<CorrelationMiddleware>();
        app.UseExceptionHandler();
        return app;
    }

    /// <summary>Maps separate liveness and readiness endpoints.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The application.</returns>
    public static WebApplication MapCommerceHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = registration => registration.Tags.Contains("live") });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = registration => !registration.Tags.Contains("live") });
        return app;
    }
}
