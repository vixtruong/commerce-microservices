using Catalog.Api.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Api.Controllers;

namespace Architecture.Tests;

/// <summary>Protects resource-location route generation across public Commerce APIs.</summary>
public sealed class ApiRouteTests
{
    /// <summary>
    /// Verifies that named Catalog and Ordering routes generate the locations returned after resource creation.
    /// </summary>
    /// <returns>A task representing the route verification.</returns>
    [Fact]
    public async Task ResourceRoutes_WhenMapped_GenerateExpectedLocationsAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(ProductsController).Assembly)
            .AddApplicationPart(typeof(OrdersController).Assembly);

        await using WebApplication application = builder.Build();
        application.MapControllers();

        // Starting on an ephemeral loopback port materializes MVC endpoints for LinkGenerator.
        application.Urls.Add("http://127.0.0.1:0");
        await application.StartAsync();

        LinkGenerator links = application.Services.GetRequiredService<LinkGenerator>();
        Guid productId = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();

        string? productLocation = links.GetPathByName(
            ProductsController.GetProductRouteName,
            new { productId });
        string? orderLocation = links.GetPathByName(
            OrdersController.GetOrderRouteName,
            new { orderId });

        Assert.Equal($"/api/catalog/products/{productId}", productLocation);
        Assert.Equal($"/api/orders/{orderId}", orderLocation);
    }
}
