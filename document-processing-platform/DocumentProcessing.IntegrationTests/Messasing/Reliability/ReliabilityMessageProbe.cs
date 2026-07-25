using DocumentProcessing.Contracts.Messaging;
using DocumentProcessing.Messaging.RabbitMq.Consuming;

namespace DocumentProcessing.IntegrationTests
    .Messaging.Reliability;

internal sealed class ReliabilityMessageProbe
{
    private readonly object _syncRoot =
        new();

    private readonly int _expectedMessageCount;

    private readonly List<ReceivedReliabilityMessage>
        _messages =
        [];

    private readonly TaskCompletionSource<
        IReadOnlyList<ReceivedReliabilityMessage>>
        _completedSource =
        new(
            TaskCreationOptions
                .RunContinuationsAsynchronously);

    public ReliabilityMessageProbe(
        int expectedMessageCount)
    {
        if (expectedMessageCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedMessageCount),
                expectedMessageCount,
                "Expected message count must be greater than zero.");
        }

        _expectedMessageCount =
            expectedMessageCount;
    }

    public void Record(
        MessageEnvelope<ReliabilityTestRequested> envelope,
        RabbitMqDeliveryContext delivery)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(delivery);

        IReadOnlyList<ReceivedReliabilityMessage>?
            completedMessages =
            null;

        lock (_syncRoot)
        {
            _messages.Add(
                new ReceivedReliabilityMessage(
                    Envelope:
                        envelope,

                    Delivery:
                        delivery,

                    ReceivedAtUtc:
                        DateTimeOffset.UtcNow));

            if (_messages.Count >=
                _expectedMessageCount)
            {
                completedMessages =
                    _messages.ToArray();
            }
        }

        if (completedMessages is not null)
        {
            _completedSource.TrySetResult(
                completedMessages);
        }
    }

    public Task<
        IReadOnlyList<ReceivedReliabilityMessage>>
        WaitAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
    {
        return _completedSource.Task.WaitAsync(
            timeout,
            cancellationToken);
    }
}

internal sealed record ReceivedReliabilityMessage(
    MessageEnvelope<ReliabilityTestRequested> Envelope,
    RabbitMqDeliveryContext Delivery,
    DateTimeOffset ReceivedAtUtc);