using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Infrastructure.Messaging;
using Commerce.BuildingBlocks.Infrastructure.Health;
using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payment.Application.Payments;
using Payment.Infrastructure.Payments;
using Payment.Infrastructure.Persistence;

namespace Payment.Infrastructure;

/// <summary>Registers Payment persistence, fake provider, Inbox, Outbox, and RabbitMQ.</summary>
public static class DependencyInjection
{
    /// <summary>Adds Payment infrastructure.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Service configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPaymentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string database = configuration.GetConnectionString("PaymentDb")
            ?? throw new InvalidOperationException("Connection string 'PaymentDb' was not configured.");
        services.AddDbContext<PaymentDbContext>(options => options.UseNpgsql(database, npgsql => npgsql.EnableRetryOnFailure(3)));
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork<PaymentDbContext>>();
        services.Configure<FakePaymentOptions>(configuration.GetSection("FakePayment"));
        services.AddScoped<IPaymentGateway, FakePaymentGateway>();
        services.AddCommerceMessaging(configuration);
        services.AddTransactionalMessaging<PaymentDbContext>();
        services.AddOutboxProcessor<PaymentDbContext>();
        services.AddPostgresReadiness<PaymentDbContext>();
        services.AddRabbitMqReadiness();
        return services;
    }
}
