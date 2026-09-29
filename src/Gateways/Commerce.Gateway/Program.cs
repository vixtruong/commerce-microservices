using System.Threading.RateLimiting;
using Commerce.BuildingBlocks.Infrastructure.Http;
using Commerce.BuildingBlocks.Infrastructure.Logging;
using Commerce.BuildingBlocks.Infrastructure.Observability;
using Commerce.BuildingBlocks.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);
builder.AddCommerceSerilog();
builder.AddCommerceOpenTelemetry();
builder.Services.AddCommerceHttpDefaults();
builder.Services.AddCommerceJwt(builder.Configuration);
builder.Services.AddCors(options => options.AddPolicy("commerce-clients", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetTokenBucketLimiter(
            context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 100,
                TokensPerPeriod = 50,
                ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                QueueLimit = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));
});
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.UseCommerceHttpDefaults();
app.UseCommerceRequestLogging();
app.UseCors("commerce-clients");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapCommerceHealthChecks();
app.MapGet("/", () => Results.Ok(new { service = "commerce-gateway", architecture = "YARP REST edge; gRPC is internal only" }));
app.MapReverseProxy();
await app.RunAsync();

/// <summary>Exposes the web entry point for architecture and integration tests.</summary>
public partial class Program;
