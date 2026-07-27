using Rabbit.Messaging.Abstractions;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Messaging.RabbitMq.Consuming;

namespace DocumentProcessing.IntegrationTests
    .Messaging.Reliability;

internal sealed class RetryOnceMessageHandler :
    IRabbitMqMessageHandler<ReliabilityTestRequested>
{
    private readonly IMessageRetryScheduler _retryScheduler;
    private readonly ReliabilityMessageProbe _probe;

    public RetryOnceMessageHandler(
        IMessageRetryScheduler retryScheduler,
        ReliabilityMessageProbe probe)
    {
        ArgumentNullException.ThrowIfNull(retryScheduler);
        ArgumentNullException.ThrowIfNull(probe);

        _retryScheduler =
            retryScheduler;

        _probe =
            probe;
    }

    public async Task<RabbitMqMessageHandlingResult> HandleAsync(
        MessageEnvelope<ReliabilityTestRequested> envelope,
        RabbitMqDeliveryContext delivery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(delivery);

        _probe.Record(
            envelope,
            delivery);

        if (envelope.Attempt == 1)
        {
            await _retryScheduler.ScheduleRetryAsync(
                envelope,
                TimeSpan.FromSeconds(
                    RabbitMqReliabilityTestNames
                        .RetryDelaySeconds),
                cancellationToken);

            /*
             * Yeni retry mesajı publisher confirm aldı.
             * İlk fiziksel mesaj ACK edilebilir.
             */
            return RabbitMqMessageHandlingResult.Acknowledge(
                "Delayed retry was scheduled.");
        }

        return RabbitMqMessageHandlingResult.Acknowledge(
            "Retried message was processed.");
    }
}