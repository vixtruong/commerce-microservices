using Commerce.BuildingBlocks.Infrastructure.Http;
using Commerce.BuildingBlocks.Infrastructure.Logging;
using Commerce.BuildingBlocks.Infrastructure.Observability;
using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Commerce.BuildingBlocks.Infrastructure.Security;
using Identity.Infrastructure;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.AddCommerceSerilog();
builder.AddCommerceOpenTelemetry();
builder.Services.AddCommerceHttpDefaults();
builder.Services.AddCommerceJwt(builder.Configuration);
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync<IdentityDbContext>();
}
app.UseCommerceHttpDefaults();
app.UseCommerceRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.MapCommerceHealthChecks();

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("DevelopmentSeed:Enabled"))
{
    using IServiceScope scope = app.Services.CreateScope();
    string customerPassword = builder.Configuration["DevelopmentSeed:CustomerPassword"]
        ?? throw new InvalidOperationException("DevelopmentSeed:CustomerPassword is required when seeding is enabled.");
    string adminPassword = builder.Configuration["DevelopmentSeed:AdminPassword"]
        ?? throw new InvalidOperationException("DevelopmentSeed:AdminPassword is required when seeding is enabled.");
    UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    RoleManager<IdentityRole<Guid>> roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    await IdentityDevelopmentData.SeedAsync(users, roles, customerPassword, adminPassword);
}

await app.RunAsync();

/// <summary>Exposes the Identity entry point for integration tests.</summary>
public partial class Program;
