using Commerce.BuildingBlocks.Infrastructure.Http;
using Commerce.BuildingBlocks.Infrastructure.Logging;
using Commerce.BuildingBlocks.Infrastructure.Messaging;
using Commerce.BuildingBlocks.Infrastructure.Observability;
using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Notification.Application.Notifications;
using Notification.Infrastructure;
using Notification.Infrastructure.Persistence;
using Ordering.Contracts.IntegrationEvents;
using Payment.Contracts.IntegrationEvents;
using Shipping.Contracts.IntegrationEvents;

var builder = WebApplication.CreateBuilder(args);
builder.AddCommerceSerilog();
builder.AddCommerceOpenTelemetry();
builder.Services.AddCommerceHttpDefaults();
builder.Services.AddNotificationInfrastructure(builder.Configuration);
builder.Services.AddIntegrationEventConsumer<OrderCreatedIntegrationEventV1, NotificationHandler>(
    "notification.order-created.v1", OrderingEventNames.OrderCreatedV1);
builder.Services.AddIntegrationEventConsumer<OrderCancelledIntegrationEventV1, NotificationHandler>(
    "notification.order-cancelled.v1", OrderingEventNames.OrderCancelledV1);
builder.Services.AddIntegrationEventConsumer<PaymentSucceededIntegrationEventV1, NotificationHandler>(
    "notification.payment-succeeded.v1", PaymentEventNames.PaymentSucceededV1);
builder.Services.AddIntegrationEventConsumer<PaymentFailedIntegrationEventV1, NotificationHandler>(
    "notification.payment-failed.v1", PaymentEventNames.PaymentFailedV1);
builder.Services.AddIntegrationEventConsumer<ShipmentCreatedIntegrationEventV1, NotificationHandler>(
    "notification.shipment-created.v1", ShippingEventNames.ShipmentCreatedV1);
builder.Services.AddIntegrationEventConsumer<ShipmentDeliveredIntegrationEventV1, NotificationHandler>(
    "notification.shipment-delivered.v1", ShippingEventNames.ShipmentDeliveredV1);

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync<NotificationDbContext>();
}
app.UseCommerceHttpDefaults();
app.UseCommerceRequestLogging();
app.MapCommerceHealthChecks();
await app.RunAsync();

/// <summary>Exposes the Notification worker entry point for integration tests.</summary>
public partial class Program;
