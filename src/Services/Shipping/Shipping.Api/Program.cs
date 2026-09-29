using Commerce.BuildingBlocks.Infrastructure.Http;
using Commerce.BuildingBlocks.Infrastructure.Logging;
using Commerce.BuildingBlocks.Infrastructure.Messaging;
using Commerce.BuildingBlocks.Infrastructure.Observability;
using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Commerce.BuildingBlocks.Infrastructure.Security;
using Ordering.Contracts.IntegrationEvents;
using Shipping.Application.Shipments;
using Shipping.Infrastructure;
using Shipping.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddCommerceSerilog();
builder.AddCommerceOpenTelemetry();
builder.Services.AddCommerceHttpDefaults();
builder.Services.AddCommerceJwt(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddShippingInfrastructure(builder.Configuration);
builder.Services.AddIntegrationEventConsumer<OrderPaidIntegrationEventV1, CreateShipmentHandler>(
    "shipping.create-paid-order.v1", OrderingEventNames.OrderPaidV1);

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync<ShippingDbContext>();
}
app.UseCommerceHttpDefaults();
app.UseCommerceRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.MapCommerceHealthChecks();
await app.RunAsync();

/// <summary>Exposes the Shipping entry point for integration tests.</summary>
public partial class Program;
