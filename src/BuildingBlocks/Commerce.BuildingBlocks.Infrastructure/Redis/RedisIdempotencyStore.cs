using Commerce.BuildingBlocks.Application.Persistence;
using StackExchange.Redis;

namespace Commerce.BuildingBlocks.Infrastructure.Redis;

/// <summary>Coordinates idempotent commands with atomic Redis ownership tokens and completed responses.</summary>
public sealed class RedisIdempotencyStore : IIdempotencyStore
{
    private const string CompleteScript =
        "if redis.call('get', KEYS[1]) == ARGV[1] then redis.call('set', KEYS[1], ARGV[2], 'PX', ARGV[3]); return 1 else return 0 end";
    private const string ReleaseScript =
        "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
    private readonly IConnectionMultiplexer _redis;

    /// <summary>Initializes the idempotency store.</summary>
    /// <param name="redis">Shared Redis connection.</param>
    public RedisIdempotencyStore(IConnectionMultiplexer redis) => _redis = redis;

    /// <inheritdoc />
    public async Task<IdempotencyLease> AcquireAsync(
        string scope,
        string key,
        TimeSpan lease,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string resource = $"idempotency:v1:{scope}:{key}";
        string token = $"processing:{Guid.NewGuid():N}";
        IDatabase database = _redis.GetDatabase();
        bool acquired = await database.StringSetAsync(resource, token, lease, When.NotExists);
        if (acquired) return new IdempotencyLease(IdempotencyLeaseState.Acquired, resource, token, null);

        RedisValue current = await database.StringGetAsync(resource);
        string? value = current.IsNull ? null : current.ToString();
        return value?.StartsWith("completed:", StringComparison.Ordinal) == true
            ? new IdempotencyLease(IdempotencyLeaseState.Completed, resource, null, value[10..])
            : new IdempotencyLease(IdempotencyLeaseState.Processing, resource, null, null);
    }

    /// <inheritdoc />
    public async Task CompleteAsync(
        IdempotencyLease lease,
        string responseJson,
        TimeSpan retention,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (lease.State != IdempotencyLeaseState.Acquired || lease.OwnerToken is null)
        {
            throw new InvalidOperationException("Only an acquired idempotency lease can be completed.");
        }

        await _redis.GetDatabase().ScriptEvaluateAsync(
            CompleteScript,
            [lease.Resource],
            [lease.OwnerToken, $"completed:{responseJson}", checked((long)retention.TotalMilliseconds)]);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(IdempotencyLease lease, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (lease.State == IdempotencyLeaseState.Acquired && lease.OwnerToken is not null)
        {
            await _redis.GetDatabase().ScriptEvaluateAsync(ReleaseScript, [lease.Resource], [lease.OwnerToken]);
        }
    }
}
