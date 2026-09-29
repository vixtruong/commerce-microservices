using Commerce.BuildingBlocks.Infrastructure.Http;
using Commerce.BuildingBlocks.Infrastructure.Logging;
using Commerce.BuildingBlocks.Infrastructure.Messaging;
using Commerce.BuildingBlocks.Infrastructure.Observability;
using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Commerce.BuildingBlocks.Infrastructure.Security;
using Inventory.Contracts.IntegrationEvents;
using Ordering.Application;
using Ordering.Application.Checkout;
using Ordering.Infrastructure;
using Ordering.Infrastructure.Persistence;
using Payment.Contracts.IntegrationEvents;
using Shipping.Contracts.IntegrationEvents;

var builder = WebApplication.CreateBuilder(args);
builder.AddCommerceSerilog();
builder.AddCommerceOpenTelemetry();
builder.Services.AddCommerceHttpDefaults();
builder.Services.AddCommerceJwt(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddOrderingApplication();
builder.Services.AddOrderingInfrastructure(builder.Configuration);
builder.Services.AddIntegrationEventConsumer<InventoryReservedIntegrationEventV1, CheckoutSagaHandler>(
    "ordering.inventory-reserved.v1", InventoryEventNames.InventoryReservedV1);
builder.Services.AddIntegrationEventConsumer<InventoryReservationFailedIntegrationEventV1, CheckoutSagaHandler>(
    "ordering.inventory-failed.v1", InventoryEventNames.InventoryReservationFailedV1);
builder.Services.AddIntegrationEventConsumer<InventoryReservationExpiredIntegrationEventV1, CheckoutSagaHandler>(
    "ordering.inventory-expired.v1", InventoryEventNames.InventoryReservationExpiredV1);
builder.Services.AddIntegrationEventConsumer<InventoryReleasedIntegrationEventV1, CheckoutSagaHandler>(
    "ordering.inventory-released.v1", InventoryEventNames.InventoryReleasedV1);
builder.Services.AddIntegrationEventConsumer<PaymentSucceededIntegrationEventV1, CheckoutSagaHandler>(
    "ordering.payment-succeeded.v1", PaymentEventNames.PaymentSucceededV1);
builder.Services.AddIntegrationEventConsumer<PaymentFailedIntegrationEventV1, CheckoutSagaHandler>(
    "ordering.payment-failed.v1", PaymentEventNames.PaymentFailedV1);
builder.Services.AddIntegrationEventConsumer<ShipmentCreatedIntegrationEventV1, CheckoutSagaHandler>(
    "ordering.shipment-created.v1", ShippingEventNames.ShipmentCreatedV1);
builder.Services.AddIntegrationEventConsumer<ShipmentDeliveredIntegrationEventV1, CheckoutSagaHandler>(
    "ordering.shipment-delivered.v1", ShippingEventNames.ShipmentDeliveredV1);

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync<OrderingDbContext>();
}
app.UseCommerceHttpDefaults();
app.UseCommerceRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.MapCommerceHealthChecks();
await app.RunAsync();

/// <summary>Exposes the Ordering entry point for integration tests.</summary>
public partial class Program;
