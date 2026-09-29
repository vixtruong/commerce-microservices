using Commerce.BuildingBlocks.Infrastructure.Http;
using Commerce.BuildingBlocks.Infrastructure.Logging;
using Commerce.BuildingBlocks.Infrastructure.Messaging;
using Commerce.BuildingBlocks.Infrastructure.Observability;
using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Commerce.BuildingBlocks.Infrastructure.Security;
using Inventory.Application.Stock;
using Inventory.Infrastructure;
using Inventory.Infrastructure.Persistence;
using Ordering.Contracts.IntegrationEvents;

var builder = WebApplication.CreateBuilder(args);
builder.AddCommerceSerilog();
builder.AddCommerceOpenTelemetry();
builder.Services.AddCommerceHttpDefaults();
builder.Services.AddCommerceJwt(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInventoryInfrastructure(builder.Configuration);
builder.Services.AddIntegrationEventConsumer<InventoryReservationRequestedIntegrationEventV1, ReserveInventoryHandler>(
    "inventory.reserve-order.v1", OrderingEventNames.InventoryReservationRequestedV1);
builder.Services.AddIntegrationEventConsumer<InventoryReleaseRequestedIntegrationEventV1, ReleaseInventoryHandler>(
    "inventory.release-order.v1", OrderingEventNames.InventoryReleaseRequestedV1);
builder.Services.AddIntegrationEventConsumer<OrderPaidIntegrationEventV1, ConfirmInventoryHandler>(
    "inventory.confirm-paid-order.v1", OrderingEventNames.OrderPaidV1);

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync<InventoryDbContext>();
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    await InventoryDevelopmentData.SeedAsync(scope.ServiceProvider.GetRequiredService<InventoryDbContext>());
}
app.UseCommerceHttpDefaults();
app.UseCommerceRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.MapCommerceHealthChecks();
await app.RunAsync();

/// <summary>Exposes the Inventory entry point for integration tests.</summary>
public partial class Program;
