using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Contracts.Messaging;
using Ordering.Contracts.IntegrationEvents;
using Payment.Application.Payments;
using Payment.Domain.Payments;

namespace Payment.Application.Tests;

/// <summary>Verifies Payment consumer idempotency without infrastructure implementations.</summary>
public sealed class ProcessPaymentHandlerTests
{
    /// <summary>Verifies an Inbox replay never reaches the payment gateway or persistence layer.</summary>
    [Fact]
    public async Task HandleAsync_ProcessedMessage_DoesNotChargeAgain()
    {
        var repository = new PaymentRepositoryFake();
        var gateway = new PaymentGatewayFake();
        var inbox = new InboxFake { IsProcessed = true };
        var unitOfWork = new UnitOfWorkFake();
        var handler = new ProcessPaymentHandler(repository, gateway, inbox, new OutboxFake(), unitOfWork);

        await handler.HandleAsync(CreateEvent(), CancellationToken.None);

        Assert.Equal(0, gateway.ChargeCount);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    /// <summary>Verifies a differently identified redelivery for an existing OrderId cannot create a second charge.</summary>
    [Fact]
    public async Task HandleAsync_ExistingOrderPayment_MarksInboxWithoutChargingAgain()
    {
        PaymentRequestedIntegrationEventV1 integrationEvent = CreateEvent();
        var repository = new PaymentRepositoryFake
        {
            Existing = PaymentRecord.Create(integrationEvent.OrderId, integrationEvent.Amount, integrationEvent.Currency, DateTimeOffset.UtcNow).Value
        };
        var gateway = new PaymentGatewayFake();
        var inbox = new InboxFake();
        var unitOfWork = new UnitOfWorkFake();
        var handler = new ProcessPaymentHandler(repository, gateway, inbox, new OutboxFake(), unitOfWork);

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        Assert.Equal(0, gateway.ChargeCount);
        Assert.Equal(integrationEvent.MessageId, inbox.MarkedMessageId);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    /// <summary>Creates a valid, versioned payment request contract.</summary>
    /// <returns>A payment request event.</returns>
    private static PaymentRequestedIntegrationEventV1 CreateEvent() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
        Guid.NewGuid(), 1999m, "USD");

    /// <summary>Provides Payment repository behavior required by the handler.</summary>
    private sealed class PaymentRepositoryFake : IPaymentRepository
    {
        /// <summary>Gets or sets an existing idempotent payment.</summary>
        public PaymentRecord? Existing { get; set; }

        /// <inheritdoc />
        public Task<PaymentRecord?> GetByOrderIdAsync(Guid orderId, bool tracked, CancellationToken cancellationToken) => Task.FromResult(Existing);

        /// <inheritdoc />
        public void Add(PaymentRecord payment) => Existing = payment;
    }

    /// <summary>Counts provider calls made by the application handler.</summary>
    private sealed class PaymentGatewayFake : IPaymentGateway
    {
        /// <summary>Gets the provider call count.</summary>
        public int ChargeCount { get; private set; }

        /// <inheritdoc />
        public Task<PaymentGatewayResult> ChargeAsync(Guid orderId, decimal amount, string currency, CancellationToken cancellationToken)
        {
            ChargeCount++;
            return Task.FromResult(new PaymentGatewayResult(true, "test-reference", null));
        }
    }

    /// <summary>Provides controllable Inbox state.</summary>
    private sealed class InboxFake : IInbox
    {
        /// <summary>Gets or sets whether the message was already handled.</summary>
        public bool IsProcessed { get; set; }

        /// <summary>Gets the message most recently marked processed.</summary>
        public Guid? MarkedMessageId { get; private set; }

        /// <inheritdoc />
        public Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken) => Task.FromResult(IsProcessed);

        /// <inheritdoc />
        public void MarkProcessed(Guid messageId, string consumer) => MarkedMessageId = messageId;
    }

    /// <summary>Ignores events for tests that exercise only idempotent early exits.</summary>
    private sealed class OutboxFake : IOutbox
    {
        /// <inheritdoc />
        public void Add<TEvent>(TEvent integrationEvent, string type) where TEvent : IIntegrationEvent
        {
        }
    }

    /// <summary>Counts commits performed by the handler.</summary>
    private sealed class UnitOfWorkFake : IUnitOfWork
    {
        /// <summary>Gets the save count.</summary>
        public int SaveCount { get; private set; }

        /// <inheritdoc />
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
