using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Commerce.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Configures consistent tracing and metrics export for every Commerce process.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>Adds ASP.NET Core, HTTP client and runtime telemetry with OTLP export.</summary>
    /// <param name="builder">Application builder whose service name identifies telemetry.</param>
    /// <returns>The same builder for chaining.</returns>
    public static WebApplicationBuilder AddCommerceOpenTelemetry(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource("Commerce.Messaging")
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter());

        return builder;
    }
}
