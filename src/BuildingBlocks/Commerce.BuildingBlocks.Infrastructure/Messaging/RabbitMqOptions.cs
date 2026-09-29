namespace Commerce.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Configures the shared durable RabbitMQ exchange and connection.
/// </summary>
public sealed class RabbitMqOptions
{
    /// <summary>Gets or sets the RabbitMQ connection string.</summary>
    public string ConnectionString { get; set; } = "amqp://guest:guest@localhost:5672";

    /// <summary>Gets or sets the durable topic exchange used for integration events.</summary>
    public string ExchangeName { get; set; } = "commerce.events";

    /// <summary>Gets or sets the dead-letter exchange.</summary>
    public string DeadLetterExchangeName { get; set; } = "commerce.events.dlx";

    /// <summary>Gets or sets the maximum delivery attempts before dead lettering.</summary>
    public int MaxDeliveryAttempts { get; set; } = 5;
}
