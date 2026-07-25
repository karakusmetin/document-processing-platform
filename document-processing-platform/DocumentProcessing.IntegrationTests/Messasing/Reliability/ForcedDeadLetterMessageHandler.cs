using DocumentProcessing.Contracts.Messaging;
using DocumentProcessing.Messaging.RabbitMq.Consuming;

namespace DocumentProcessing.IntegrationTests
    .Messaging.Reliability;

internal sealed class ForcedDeadLetterMessageHandler :
    IRabbitMqMessageHandler<ReliabilityTestRequested>
{
    private readonly ReliabilityMessageProbe _probe;

    public ForcedDeadLetterMessageHandler(
        ReliabilityMessageProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        _probe =
            probe;
    }

    public Task<RabbitMqMessageHandlingResult> HandleAsync(
        MessageEnvelope<ReliabilityTestRequested> envelope,
        RabbitMqDeliveryContext delivery,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _probe.Record(
            envelope,
            delivery);

        return Task.FromResult(
            RabbitMqMessageHandlingResult.DeadLetter(
                failureCode:
                    "integration-test.forced-dead-letter",

                reason:
                    "The integration test deliberately rejected " +
                    "the message.",

                diagnosticId:
                    Guid.NewGuid().ToString("N")));
    }
}