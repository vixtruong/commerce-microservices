namespace Commerce.BuildingBlocks.Application.Persistence;

/// <summary>
/// Defines the transaction boundary used by application use cases to persist aggregate changes.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all changes made during the current use case.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Signals that optimistic concurrency rejected a stale aggregate write.</summary>
public sealed class OptimisticConcurrencyException : Exception
{
    /// <summary>Initializes the application-neutral concurrency exception.</summary>
    /// <param name="message">Conflict description.</param>
    /// <param name="innerException">Provider-specific cause.</param>
    public OptimisticConcurrencyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
