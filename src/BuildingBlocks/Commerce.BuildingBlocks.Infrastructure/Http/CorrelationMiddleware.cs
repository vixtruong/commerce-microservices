using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Commerce.BuildingBlocks.Infrastructure.Http;

/// <summary>Normalizes a correlation identifier for proxying and downstream services.</summary>
public sealed class CorrelationMiddleware
{
    /// <summary>Header carrying the business-operation correlation identifier.</summary>
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    /// <summary>Initializes the middleware.</summary>
    /// <param name="next">Next request delegate.</param>
    public CorrelationMiddleware(RequestDelegate next) => _next = next;

    /// <summary>Validates or creates a correlation identifier and forwards the request.</summary>
    /// <param name="context">Current HTTP context.</param>
    /// <returns>A task representing the request pipeline.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = context.Request.Headers.TryGetValue(HeaderName, out var supplied) &&
            Guid.TryParse(supplied.ToString(), out Guid parsed)
                ? parsed.ToString("D")
                : Guid.NewGuid().ToString("D");
        context.Request.Headers[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        Activity.Current?.SetBaggage("correlation.id", correlationId);
        await _next(context);
    }
}
