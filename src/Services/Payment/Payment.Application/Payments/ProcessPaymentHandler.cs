using Commerce.BuildingBlocks.Application.Persistence;
using Commerce.BuildingBlocks.Contracts.Messaging;
using Ordering.Contracts.IntegrationEvents;
using Payment.Contracts.IntegrationEvents;
using Payment.Domain.Payments;

namespace Payment.Application.Payments;

/// <summary>Defines Payment-owned aggregate persistence.</summary>
public interface IPaymentRepository
{
    /// <summary>Gets an existing payment by its Order idempotency key.</summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="tracked">Whether the aggregate will be mutated.</param>
    /// <param name="cancellationToken">Token used to cancel database I/O.</param>
    /// <returns>The payment, or null.</returns>
    Task<PaymentRecord?> GetByOrderIdAsync(Guid orderId, bool tracked, CancellationToken cancellationToken);

    /// <summary>Adds a new payment.</summary>
    /// <param name="payment">Payment aggregate.</param>
    void Add(PaymentRecord payment);
}

/// <summary>Processes idempotent payment requests and writes exactly one logical outcome through the Outbox.</summary>
public sealed class ProcessPaymentHandler : IIntegrationEventHandler<PaymentRequestedIntegrationEventV1>
{
    private const string ConsumerName = "payment.process-order.v1";
    private readonly IPaymentRepository _payments;
    private readonly IPaymentGateway _gateway;
    private readonly IInbox _inbox;
    private readonly IOutbox _outbox;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initializes the payment consumer.</summary>
    /// <param name="payments">Payment repository.</param>
    /// <param name="gateway">External payment port.</param>
    /// <param name="inbox">Durable deduplication store.</param>
    /// <param name="outbox">Transactional outcome writer.</param>
    /// <param name="unitOfWork">Payment transaction boundary.</param>
    public ProcessPaymentHandler(
        IPaymentRepository payments,
        IPaymentGateway gateway,
        IInbox inbox,
        IOutbox outbox,
        IUnitOfWork unitOfWork)
    {
        _payments = payments;
        _gateway = gateway;
        _inbox = inbox;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task HandleAsync(PaymentRequestedIntegrationEventV1 integrationEvent, CancellationToken cancellationToken)
    {
        if (await _inbox.HasProcessedAsync(integrationEvent.MessageId, ConsumerName, cancellationToken)) return;
        PaymentRecord? existing = await _payments.GetByOrderIdAsync(integrationEvent.OrderId, tracked: true, cancellationToken);
        if (existing is not null)
        {
            // OrderId is the durable provider idempotency key; another message cannot create another charge.
            _inbox.MarkProcessed(integrationEvent.MessageId, ConsumerName);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        PaymentRecord payment = PaymentRecord.Create(
            integrationEvent.OrderId, integrationEvent.Amount, integrationEvent.Currency, DateTimeOffset.UtcNow).Value;
        payment.StartProcessing();
        _payments.Add(payment);

        PaymentGatewayResult gatewayResult;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            gatewayResult = await _gateway.ChargeAsync(
                integrationEvent.OrderId, integrationEvent.Amount, integrationEvent.Currency, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            gatewayResult = new PaymentGatewayResult(false, null, "provider-timeout");
        }

        if (gatewayResult.Succeeded)
        {
            payment.Succeed(gatewayResult.TransactionReference!, DateTimeOffset.UtcNow);
            var succeeded = new PaymentSucceededIntegrationEventV1(
                Guid.NewGuid(), integrationEvent.CorrelationId, integrationEvent.MessageId, DateTimeOffset.UtcNow,
                integrationEvent.OrderId, payment.Id.Value, payment.TransactionReference!);
            _outbox.Add(succeeded, PaymentEventNames.PaymentSucceededV1);
        }
        else
        {
            payment.Fail(gatewayResult.FailureCode ?? "provider-failed", DateTimeOffset.UtcNow);
            var failed = new PaymentFailedIntegrationEventV1(
                Guid.NewGuid(), integrationEvent.CorrelationId, integrationEvent.MessageId, DateTimeOffset.UtcNow,
                integrationEvent.OrderId, payment.Id.Value, payment.FailureCode!);
            _outbox.Add(failed, PaymentEventNames.PaymentFailedV1);
        }

        _inbox.MarkProcessed(integrationEvent.MessageId, ConsumerName);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
