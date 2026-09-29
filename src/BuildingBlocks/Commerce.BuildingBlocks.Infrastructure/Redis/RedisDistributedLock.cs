using StackExchange.Redis;

namespace Commerce.BuildingBlocks.Infrastructure.Redis;

/// <summary>
/// Provides a token-safe Redis lock for coordination tasks that do not own core data correctness.
/// </summary>
public interface IDistributedLock
{
    /// <summary>Attempts to acquire a lock for a bounded lease.</summary>
    /// <param name="resource">Namespaced lock resource.</param>
    /// <param name="lease">Maximum lease duration.</param>
    /// <param name="cancellationToken">Token used to cancel Redis I/O.</param>
    /// <returns>A lease when acquired; otherwise, null.</returns>
    Task<IAsyncDisposable?> TryAcquireAsync(string resource, TimeSpan lease, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements distributed coordination using SET NX and compare-and-delete release semantics.
/// </summary>
public sealed class RedisDistributedLock : IDistributedLock
{
    private const string ReleaseScript =
        "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
    private readonly IConnectionMultiplexer _redis;

    /// <summary>Initializes the Redis distributed lock.</summary>
    /// <param name="redis">Shared Redis connection.</param>
    public RedisDistributedLock(IConnectionMultiplexer redis) => _redis = redis;

    /// <inheritdoc />
    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string resource,
        TimeSpan lease,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string token = Guid.NewGuid().ToString("N");
        string key = $"lock:v1:{resource}";
        bool acquired = await _redis.GetDatabase().StringSetAsync(key, token, lease, When.NotExists);
        return acquired ? new RedisLockLease(_redis.GetDatabase(), key, token) : null;
    }

    /// <summary>Releases a lock only when the owner token still matches.</summary>
    private sealed class RedisLockLease : IAsyncDisposable
    {
        private readonly IDatabase _database;
        private readonly string _key;
        private readonly string _token;

        /// <summary>Initializes the lock lease.</summary>
        /// <param name="database">Redis database.</param>
        /// <param name="key">Lock key.</param>
        /// <param name="token">Unique owner token.</param>
        public RedisLockLease(IDatabase database, string key, string token)
        {
            _database = database;
            _key = key;
            _token = token;
        }

        /// <summary>Releases the owned lock.</summary>
        /// <returns>A task that completes after the atomic comparison.</returns>
        public async ValueTask DisposeAsync() =>
            await _database.ScriptEvaluateAsync(ReleaseScript, [_key], [_token]);
    }
}
