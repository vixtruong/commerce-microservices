using Microsoft.Extensions.DependencyInjection;

namespace Ordering.Application;

/// <summary>Registers Ordering commands, queries, and Saga handlers.</summary>
public static class DependencyInjection
{
    /// <summary>Adds MediatR handlers from the Ordering application assembly.</summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddOrderingApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}
