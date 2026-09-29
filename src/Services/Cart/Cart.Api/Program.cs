using Cart.Api.Services;
using Cart.Infrastructure;
using Commerce.BuildingBlocks.Infrastructure.Http;
using Commerce.BuildingBlocks.Infrastructure.Logging;
using Commerce.BuildingBlocks.Infrastructure.Observability;
using Commerce.BuildingBlocks.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);
builder.AddCommerceSerilog();
builder.AddCommerceOpenTelemetry();
builder.Services.AddCommerceHttpDefaults();
builder.Services.AddCommerceJwt(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddGrpc();
builder.Services.AddCartInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseCommerceHttpDefaults();
app.UseCommerceRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.MapGrpcService<CartInternalGrpcService>();
app.MapCommerceHealthChecks();
await app.RunAsync();

/// <summary>Exposes the Cart entry point for integration tests.</summary>
public partial class Program;
