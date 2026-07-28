namespace DocumentProcessing.Worker.Consumers.Retry;

internal interface IRetryDelayProvider
{
    int MaximumAttempts { get; }

    bool TryGetNextDelay(
        int currentAttempt,
        out TimeSpan delay);
}