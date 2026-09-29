using Inventory.Application.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Inventory.Infrastructure;

/// <summary>Configures bounded polling for abandoned inventory reservations.</summary>
public sealed class ReservationExpirationOptions
{
    /// <summary>Gets or sets the delay between expiry scans in seconds.</summary>
    public int IntervalSeconds { get; set; } = 10;

    /// <summary>Gets or sets the maximum stock aggregate count processed per scan.</summary>
    public int BatchSize { get; set; } = 100;
}

/// <summary>Periodically expires abandoned holds through the Inventory application service.</summary>
public sealed class ReservationExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReservationExpirationOptions _options;
    private readonly ILogger<ReservationExpirationWorker> _logger;

    /// <summary>Initializes the reservation expiration worker.</summary>
    /// <param name="scopeFactory">Factory used to create one transaction scope per scan.</param>
    /// <param name="options">Bounded polling options.</param>
    /// <param name="logger">Structured logger.</param>
    public ReservationExpirationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ReservationExpirationOptions> options,
        ILogger<ReservationExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Runs bounded expiry scans until the host stops.</summary>
    /// <param name="stoppingToken">Host shutdown token.</param>
    /// <returns>A task representing the worker lifetime.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromSeconds(Math.Clamp(_options.IntervalSeconds, 1, 3600));
        int batchSize = Math.Clamp(_options.BatchSize, 1, 1000);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
                ReservationExpirationService service =
                    scope.ServiceProvider.GetRequiredService<ReservationExpirationService>();
                int expiredCount = await service.ExpireBatchAsync(
                    DateTimeOffset.UtcNow, batchSize, stoppingToken);
                if (expiredCount > 0)
                {
                    _logger.LogInformation("Expired {ReservationCount} inventory reservations.", expiredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (DbUpdateConcurrencyException exception)
            {
                // Another instance won the optimistic update; the next scan safely reloads current state.
                _logger.LogWarning(exception, "Inventory reservation expiry encountered a concurrent update.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Inventory reservation expiry scan failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
