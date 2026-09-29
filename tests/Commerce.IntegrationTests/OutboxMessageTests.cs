using Commerce.BuildingBlocks.Infrastructure.Persistence;
using Ordering.Contracts.IntegrationEvents;

namespace Commerce.IntegrationTests;

/// <summary>Verifies transport metadata survives the transactional outbox boundary.</summary>
public sealed class OutboxMessageTests
{
    /// <summary>Verifies serialization preserves message, correlation, causation and versioned routing data.</summary>
    [Fact]
    public void From_IntegrationEvent_PreservesEnvelopeMetadata()
    {
        Guid messageId = Guid.NewGuid();
        Guid correlationId = Guid.NewGuid();
        Guid causationId = Guid.NewGuid();
        var integrationEvent = new OrderCreatedIntegrationEventV1(
            messageId, correlationId, causationId, DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid(), "ORD-1");

        OutboxMessage message = OutboxMessage.From(integrationEvent, OrderingEventNames.OrderCreatedV1);
        var envelope = message.ToEnvelope();

        Assert.Equal(messageId, envelope.MessageId);
        Assert.Equal(correlationId, envelope.CorrelationId);
        Assert.Equal(causationId, envelope.CausationId);
        Assert.Equal(OrderingEventNames.OrderCreatedV1, envelope.Type);
        Assert.Contains("ORD-1", envelope.Payload, StringComparison.Ordinal);
    }
}
