using Catalog.Api.Services;
using Catalog.Application;
using Catalog.Infrastructure;
using Commerce.BuildingBlocks.Infrastructure.Http;
using Commerce.BuildingBlocks.Infrastructure.Logging;
using Commerce.BuildingBlocks.Infrastructure.Observability;
using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Commerce.BuildingBlocks.Infrastructure.Security;
using Catalog.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddCommerceSerilog();
builder.AddCommerceOpenTelemetry();
builder.Services.AddCommerceHttpDefaults();
builder.Services.AddCommerceJwt(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddGrpc();
builder.Services.AddCatalogApplication(builder.Configuration["MediatR:LicenseKey"]);
builder.Services.AddCatalogInfrastructure(builder.Configuration);

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync<CatalogDbContext>();
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    await CatalogDevelopmentData.SeedAsync(scope.ServiceProvider.GetRequiredService<CatalogDbContext>());
}
app.UseCommerceHttpDefaults();
app.UseCommerceRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Commerce-Instance"] = Environment.MachineName;
    context.Response.Headers["X-Service-Instance"] = Environment.MachineName;
    await next();
});
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.MapGrpcService<CatalogInternalGrpcService>();
app.MapCommerceHealthChecks();
await app.RunAsync();

/// <summary>Exposes the Catalog entry point for integration tests.</summary>
public partial class Program;
