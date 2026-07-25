using DocumentProcessing.Contracts.Messaging;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Worker.Consumers.Retry;

namespace DocumentProcessing.UnitTests.Worker.Consumers;

internal sealed class StubConversionOrchestrator :
    IConversionOrchestrator
{
    private readonly Func<
        ConversionRequest,
        CancellationToken,
        Task<ConversionExecutionResult>> _execute;

    public StubConversionOrchestrator(
        Func<
            ConversionRequest,
            CancellationToken,
            Task<ConversionExecutionResult>> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);

        _execute = execute;
    }

    public List<ConversionRequest> Requests { get; } =
        [];

    public Task<ConversionExecutionResult> ExecuteAsync(
        ConversionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Requests.Add(request);

        return _execute(
            request,
            cancellationToken);
    }
}

internal sealed class RecordingMessagePublisher :
    IMessagePublisher
{
    public List<PublishedMessage> Messages { get; } =
        [];

    public Task PublishAsync<TMessage>(
        TMessage message,
        MessagePublishContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        Messages.Add(
            new PublishedMessage(
                Message:
                    message,

                Context:
                    context));

        return Task.CompletedTask;
    }
}

internal sealed record PublishedMessage(
    object Message,
    MessagePublishContext Context);

internal sealed class RecordingMessageRetryScheduler :
    IMessageRetryScheduler
{
    public List<ScheduledRetry> Retries { get; } =
        [];

    public Task ScheduleRetryAsync<TMessage>(
        MessageEnvelope<TMessage> originalEnvelope,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(originalEnvelope);

        Retries.Add(
            new ScheduledRetry(
                Envelope:
                    originalEnvelope,

                Delay:
                    delay));

        return Task.CompletedTask;
    }
}

internal sealed record ScheduledRetry(
    object Envelope,
    TimeSpan Delay);

internal sealed class StubRetryDelayProvider :
    IRetryDelayProvider
{
    private readonly IReadOnlyDictionary<
        int,
        TimeSpan> _delays;

    public StubRetryDelayProvider(
        IReadOnlyDictionary<int, TimeSpan>? delays = null,
        int maximumAttempts = 4)
    {
        _delays =
            delays ??
            new Dictionary<int, TimeSpan>();

        MaximumAttempts =
            maximumAttempts;
    }

    public int MaximumAttempts { get; }

    public bool TryGetNextDelay(
        int currentAttempt,
        out TimeSpan delay)
    {
        return _delays.TryGetValue(
            currentAttempt,
            out delay);
    }
}