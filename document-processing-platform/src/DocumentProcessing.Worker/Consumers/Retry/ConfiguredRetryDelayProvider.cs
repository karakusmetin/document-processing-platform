using DocumentProcessing.Messaging.RabbitMq.Configuration;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Worker.Consumers.Retry;

internal sealed class ConfiguredRetryDelayProvider :
    IRetryDelayProvider
{
    private readonly RabbitMqRetryOptions _options;

    public ConfiguredRetryDelayProvider(
        IOptions<RabbitMqRetryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public int MaximumAttempts =>
        _options.MaximumAttempts;

    public bool TryGetNextDelay(
        int currentAttempt,
        out TimeSpan delay)
    {
        if (currentAttempt < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentAttempt),
                currentAttempt,
                "Current attempt must be greater than zero.");
        }

        /*
         * Attempt 1 için index 0:
         *     DelaySeconds[0] = 10
         *
         * Attempt 2 için index 1:
         *     DelaySeconds[1] = 60
         */
        int delayIndex =
            currentAttempt - 1;

        if (delayIndex >=
            _options.DelaySeconds.Length)
        {
            delay = default;
            return false;
        }

        delay =
            TimeSpan.FromSeconds(
                _options.DelaySeconds[delayIndex]);

        return true;
    }
}