using Commerce.BuildingBlocks.Infrastructure.Http;
using Commerce.BuildingBlocks.Infrastructure.Logging;
using Commerce.BuildingBlocks.Infrastructure.Messaging;
using Commerce.BuildingBlocks.Infrastructure.Observability;
using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Commerce.BuildingBlocks.Infrastructure.Security;
using Ordering.Contracts.IntegrationEvents;
using Payment.Application.Payments;
using Payment.Infrastructure;
using Payment.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddCommerceSerilog();
builder.AddCommerceOpenTelemetry();
builder.Services.AddCommerceHttpDefaults();
builder.Services.AddCommerceJwt(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddPaymentInfrastructure(builder.Configuration);
builder.Services.AddIntegrationEventConsumer<PaymentRequestedIntegrationEventV1, ProcessPaymentHandler>(
    "payment.process-order.v1", OrderingEventNames.PaymentRequestedV1);

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync<PaymentDbContext>();
}
app.UseCommerceHttpDefaults();
app.UseCommerceRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.MapCommerceHealthChecks();
await app.RunAsync();

/// <summary>Exposes the Payment entry point for integration tests.</summary>
public partial class Program;
