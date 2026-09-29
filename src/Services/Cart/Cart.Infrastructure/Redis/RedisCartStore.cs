using System.Text.Json;
using Cart.Application.Carts;
using StackExchange.Redis;

namespace Cart.Infrastructure.Redis;

/// <summary>Persists carts as versioned Redis JSON documents with sliding-like TTL refresh on mutation.</summary>
public sealed class RedisCartStore : ICartStore
{
    private static readonly TimeSpan CartTimeToLive = TimeSpan.FromDays(7);
    private readonly IConnectionMultiplexer _redis;

    /// <summary>Initializes the Redis cart store.</summary>
    /// <param name="redis">Shared Redis connection.</param>
    public RedisCartStore(IConnectionMultiplexer redis) => _redis = redis;

    /// <inheritdoc />
    public async Task<CustomerCart> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue value = await _redis.GetDatabase().StringGetAsync(Key(customerId));
        return value.IsNullOrEmpty
            ? new CustomerCart(customerId, [], DateTimeOffset.UtcNow)
            : JsonSerializer.Deserialize<CustomerCart>((string)value!)
                ?? new CustomerCart(customerId, [], DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task SetAsync(CustomerCart cart, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _redis.GetDatabase().StringSetAsync(Key(cart.CustomerId), JsonSerializer.Serialize(cart), CartTimeToLive);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid customerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _redis.GetDatabase().KeyDeleteAsync(Key(customerId));
    }

    /// <summary>Builds the service-owned versioned Redis key.</summary>
    /// <param name="customerId">Customer identifier.</param>
    /// <returns>A namespaced key.</returns>
    private static string Key(Guid customerId) => $"cart:v1:{customerId:D}";
}
