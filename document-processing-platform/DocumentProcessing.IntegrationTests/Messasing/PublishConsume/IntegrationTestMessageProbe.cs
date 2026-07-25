using DocumentProcessing.Contracts.Messaging;
using DocumentProcessing.Messaging.RabbitMq.Consuming;

namespace DocumentProcessing.IntegrationTests
    .Messaging.PublishConsume;

internal sealed class IntegrationTestMessageProbe
{
    private readonly TaskCompletionSource<
        ReceivedIntegrationTestMessage> _receivedSource =
        new(
            TaskCreationOptions
                .RunContinuationsAsynchronously);

    public void Record(
        MessageEnvelope<IntegrationTestRequested> envelope,
        RabbitMqDeliveryContext delivery)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(delivery);

        _receivedSource.TrySetResult(
            new ReceivedIntegrationTestMessage(
                Envelope:
                    envelope,

                Delivery:
                    delivery));
    }

    public Task<ReceivedIntegrationTestMessage>
        WaitAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
    {
        return _receivedSource.Task.WaitAsync(
            timeout,
            cancellationToken);
    }
}

internal sealed record ReceivedIntegrationTestMessage(
    MessageEnvelope<IntegrationTestRequested> Envelope,
    RabbitMqDeliveryContext Delivery);