using System.Text.Json;
using Catalog.Application.Abstractions;
using Catalog.Application.Products.Queries.GetProductById;
using StackExchange.Redis;

namespace Catalog.Infrastructure.Caching;

/// <summary>Stores serialized application DTOs in a versioned Redis cache.</summary>
public sealed class RedisProductCache : IProductCache
{
    private static readonly TimeSpan TimeToLive = TimeSpan.FromMinutes(5);
    private readonly IConnectionMultiplexer _redis;

    /// <summary>Initializes the product cache.</summary>
    /// <param name="redis">Shared Redis connection.</param>
    public RedisProductCache(IConnectionMultiplexer redis) => _redis = redis;

    /// <inheritdoc />
    public async Task<ProductResponse?> GetAsync(Guid productId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue value = await _redis.GetDatabase().StringGetAsync(Key(productId));
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<ProductResponse>((string)value!);
    }

    /// <inheritdoc />
    public async Task SetAsync(ProductResponse response, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _redis.GetDatabase().StringSetAsync(Key(response.Id), JsonSerializer.Serialize(response), TimeToLive);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Guid productId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _redis.GetDatabase().KeyDeleteAsync(Key(productId));
    }

    /// <summary>Builds the versioned Catalog cache key.</summary>
    /// <param name="productId">Product identifier.</param>
    /// <returns>A namespaced cache key.</returns>
    private static string Key(Guid productId) => $"catalog:product:v1:{productId:D}";
}
