namespace Commerce.BuildingBlocks.Contracts.Messaging;

/// <summary>
/// Publishes durable integration messages to the configured transactional broker.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an already-serialized outbox message without changing its identity metadata.
    /// </summary>
    /// <param name="message">Message envelope to publish.</param>
    /// <param name="cancellationToken">Token used to cancel broker I/O.</param>
    /// <returns>A task that completes after the broker confirms the message.</returns>
    Task PublishAsync(IntegrationMessageEnvelope message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles one deserialized integration event in an idempotent consumer transaction.
/// </summary>
/// <typeparam name="TEvent">Integration event contract type.</typeparam>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    /// <summary>Handles a broker-delivered event.</summary>
    /// <param name="integrationEvent">Event to handle.</param>
    /// <param name="cancellationToken">Token used to cancel processing.</param>
    /// <returns>A task that completes after durable processing succeeds.</returns>
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
