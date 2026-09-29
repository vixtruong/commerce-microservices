namespace Commerce.BuildingBlocks.Contracts.Messaging;

/// <summary>
/// Defines the transport metadata carried by every cross-service integration event.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Gets the unique message identifier used for durable deduplication.</summary>
    Guid MessageId { get; }

    /// <summary>Gets the identifier shared by all messages in one distributed operation.</summary>
    Guid CorrelationId { get; }

    /// <summary>Gets the message that directly caused this event, when one exists.</summary>
    Guid? CausationId { get; }

    /// <summary>Gets the UTC time at which the event occurred.</summary>
    DateTimeOffset OccurredOnUtc { get; }
}

/// <summary>
/// Describes a serialized integration message delivered by the event bus.
/// </summary>
/// <param name="MessageId">Unique message identifier.</param>
/// <param name="CorrelationId">Distributed operation identifier.</param>
/// <param name="CausationId">Identifier of the causing message.</param>
/// <param name="Type">Stable event type and version.</param>
/// <param name="Payload">UTF-8 JSON event body.</param>
/// <param name="OccurredOnUtc">UTC event occurrence time.</param>
public sealed record IntegrationMessageEnvelope(
    Guid MessageId,
    Guid CorrelationId,
    Guid? CausationId,
    string Type,
    string Payload,
    DateTimeOffset OccurredOnUtc);
