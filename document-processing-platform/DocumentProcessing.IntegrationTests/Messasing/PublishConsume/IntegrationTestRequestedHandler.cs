using DocumentProcessing.Contracts.Messaging;
using DocumentProcessing.Messaging.RabbitMq.Consuming;

namespace DocumentProcessing.IntegrationTests
    .Messaging.PublishConsume;

internal sealed class IntegrationTestRequestedHandler :
    IRabbitMqMessageHandler<IntegrationTestRequested>
{
    private readonly IntegrationTestMessageProbe _probe;

    public IntegrationTestRequestedHandler(
        IntegrationTestMessageProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        _probe = probe;
    }

    public Task<RabbitMqMessageHandlingResult> HandleAsync(
        MessageEnvelope<IntegrationTestRequested> envelope,
        RabbitMqDeliveryContext delivery,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _probe.Record(
            envelope,
            delivery);

        return Task.FromResult(
            RabbitMqMessageHandlingResult.Acknowledge(
                "Integration test message was received."));
    }
}