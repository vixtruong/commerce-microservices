namespace Commerce.BuildingBlocks.Application.Persistence;

/// <summary>Describes the result of attempting to acquire an idempotent command key.</summary>
/// <param name="State">Acquisition state.</param>
/// <param name="Resource">Namespaced Redis key.</param>
/// <param name="OwnerToken">Unique token when newly acquired.</param>
/// <param name="CompletedResponse">Previously completed JSON response.</param>
public sealed record IdempotencyLease(
    IdempotencyLeaseState State,
    string Resource,
    string? OwnerToken,
    string? CompletedResponse);

/// <summary>Defines Redis idempotency acquisition states.</summary>
public enum IdempotencyLeaseState
{
    /// <summary>The caller owns a new processing lease.</summary>
    Acquired,
    /// <summary>Another request currently owns the key.</summary>
    Processing,
    /// <summary>A previous request completed and has a reusable response.</summary>
    Completed
}

/// <summary>Provides reusable Redis-backed idempotency coordination for selected HTTP commands.</summary>
public interface IIdempotencyStore
{
    /// <summary>Attempts to acquire one scoped client idempotency key.</summary>
    /// <param name="scope">Use-case namespace.</param>
    /// <param name="key">Client-provided key.</param>
    /// <param name="lease">Maximum processing lease.</param>
    /// <param name="cancellationToken">Token used to cancel Redis I/O.</param>
    /// <returns>The acquisition state and prior response when complete.</returns>
    Task<IdempotencyLease> AcquireAsync(
        string scope,
        string key,
        TimeSpan lease,
        CancellationToken cancellationToken);

    /// <summary>Atomically replaces an owned processing marker with a completed response.</summary>
    /// <param name="lease">Owned lease.</param>
    /// <param name="responseJson">Serialized successful response.</param>
    /// <param name="retention">How long retries may reuse the response.</param>
    /// <param name="cancellationToken">Token used to cancel Redis I/O.</param>
    /// <returns>A task that completes after the token-safe update.</returns>
    Task CompleteAsync(
        IdempotencyLease lease,
        string responseJson,
        TimeSpan retention,
        CancellationToken cancellationToken);

    /// <summary>Releases a processing marker only when the caller still owns it.</summary>
    /// <param name="lease">Owned lease.</param>
    /// <param name="cancellationToken">Token used to cancel Redis I/O.</param>
    /// <returns>A task that completes after token-safe release.</returns>
    Task ReleaseAsync(IdempotencyLease lease, CancellationToken cancellationToken);
}
