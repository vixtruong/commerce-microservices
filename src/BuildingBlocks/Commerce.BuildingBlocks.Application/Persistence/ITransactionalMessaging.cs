using Commerce.BuildingBlocks.Contracts.Messaging;

namespace Commerce.BuildingBlocks.Application.Persistence;

/// <summary>Writes integration events to the current relational transaction.</summary>
public interface IOutbox
{
    /// <summary>Adds an event without publishing it before commit.</summary>
    /// <typeparam name="TEvent">Integration event contract.</typeparam>
    /// <param name="integrationEvent">Event to persist.</param>
    /// <param name="type">Stable versioned routing key.</param>
    void Add<TEvent>(TEvent integrationEvent, string type) where TEvent : IIntegrationEvent;
}

/// <summary>Tracks successfully consumed message identities in the current relational transaction.</summary>
public interface IInbox
{
    /// <summary>Determines whether a message was already processed by this consumer.</summary>
    /// <param name="messageId">Broker message identifier.</param>
    /// <param name="consumer">Stable consumer identity.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>True when processing already committed.</returns>
    Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken);

    /// <summary>Adds a success marker to be committed with the business mutation.</summary>
    /// <param name="messageId">Broker message identifier.</param>
    /// <param name="consumer">Stable consumer identity.</param>
    void MarkProcessed(Guid messageId, string consumer);
}
