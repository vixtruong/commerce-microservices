using Commerce.BuildingBlocks.Contracts.Messaging;

namespace Commerce.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Stores an integration event in the same local transaction as its aggregate changes.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Gets or sets the message identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the versioned routing key and contract name.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the serialized JSON payload.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Gets or sets the distributed correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the directly causing message identifier.</summary>
    public Guid? CausationId { get; set; }

    /// <summary>Gets or sets the UTC event occurrence time.</summary>
    public DateTimeOffset OccurredOnUtc { get; set; }

    /// <summary>Gets or sets the UTC time when publication succeeded.</summary>
    public DateTimeOffset? ProcessedOnUtc { get; set; }

    /// <summary>Gets or sets the most recent publication failure.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets the number of failed publication attempts.</summary>
    public int RetryCount { get; set; }

    /// <summary>Gets or sets the processor instance that currently owns the row.</summary>
    public Guid? LockId { get; set; }

    /// <summary>Gets or sets when an abandoned processing claim becomes available again.</summary>
    public DateTimeOffset? LockedUntilUtc { get; set; }

    /// <summary>Creates an outbox record from a typed integration event.</summary>
    /// <typeparam name="TEvent">Event contract type.</typeparam>
    /// <param name="integrationEvent">Event to serialize.</param>
    /// <param name="type">Stable versioned routing key.</param>
    /// <returns>A new unprocessed outbox message.</returns>
    public static OutboxMessage From<TEvent>(TEvent integrationEvent, string type)
        where TEvent : IIntegrationEvent
    {
        return new OutboxMessage
        {
            Id = integrationEvent.MessageId,
            Type = type,
            Payload = System.Text.Json.JsonSerializer.Serialize(integrationEvent),
            CorrelationId = integrationEvent.CorrelationId,
            CausationId = integrationEvent.CausationId,
            OccurredOnUtc = integrationEvent.OccurredOnUtc
        };
    }

    /// <summary>Creates the broker envelope while preserving message identity.</summary>
    /// <returns>The serialized integration message envelope.</returns>
    public IntegrationMessageEnvelope ToEnvelope() =>
        new(Id, CorrelationId, CausationId, Type, Payload, OccurredOnUtc);
}

/// <summary>
/// Records a successfully handled broker message to provide durable consumer deduplication.
/// </summary>
public sealed class InboxMessage
{
    /// <summary>Gets or sets the unique broker message identifier.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Gets or sets the handler or queue that processed the message.</summary>
    public string Consumer { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC completion time.</summary>
    public DateTimeOffset ProcessedOnUtc { get; set; }
}
