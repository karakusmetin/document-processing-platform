using Queue.Messaging.RabbitMq.Compatibility;
using Queue.Messaging.RabbitMq.Configuration;

namespace Queue.Messaging.RabbitMq.Retrying;

/// <summary>
/// Global RabbitMQ retry configuration ile route/endpoint
/// bazlı override değerlerinin birleştirilmiş ve doğrulanmış
/// hâlidir.
/// </summary>
internal sealed class RabbitMqEffectiveRetryPolicy
{
    private readonly int[] _delaySeconds;

    private RabbitMqEffectiveRetryPolicy(
        int maximumAttempts,
        int[] delaySeconds)
    {
        MaximumAttempts =
            maximumAttempts;

        _delaySeconds =
            delaySeconds;
    }

    /// <summary>
    /// İlk işleme dahil toplam maksimum deneme sayısıdır.
    /// </summary>
    public int MaximumAttempts { get; }

    /// <summary>
    /// Retry gecikmelerinin immutable görünümüdür.
    /// </summary>
    public IReadOnlyList<int> DelaySeconds =>
        _delaySeconds;

    public static RabbitMqEffectiveRetryPolicy Resolve(
        RabbitMqRetryOptions globalOptions,
        int? maximumAttemptsOverride,
        int[]? delaySecondsOverride)
    {
        Guard.NotNull(
            globalOptions,
            nameof(globalOptions));

        int maximumAttempts =
            maximumAttemptsOverride
            ?? globalOptions.MaximumAttempts;

        int[] sourceDelaySeconds =
            delaySecondsOverride
            ?? globalOptions.DelaySeconds
            ?? Array.Empty<int>();

        /*
         * Configuration veya endpoint options üzerinden gelen
         * array referansını doğrudan saklamıyoruz.
         *
         * Registration sonrasında dışarıdan değiştirilmesini
         * önlemek için kopyalıyoruz.
         */
        int[] delaySeconds =
            sourceDelaySeconds.ToArray();

        Validate(
            maximumAttempts,
            delaySeconds);

        return new RabbitMqEffectiveRetryPolicy(
            maximumAttempts,
            delaySeconds);
    }

    /// <summary>
    /// Mevcut attempt sonrasında uygulanması gereken retry
    /// gecikmesini döndürür.
    ///
    /// Attempt 1 → DelaySeconds[0]
    /// Attempt 2 → DelaySeconds[1]
    /// </summary>
    public int GetDelaySecondsForCurrentAttempt(
        int currentAttempt)
    {
        if (currentAttempt < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentAttempt),
                currentAttempt,
                "Current message attempt must be greater than zero.");
        }

        if (currentAttempt >= MaximumAttempts)
        {
            throw new InvalidOperationException(
                "RabbitMQ retry cannot be scheduled because the " +
                $"maximum attempt count '{MaximumAttempts}' has " +
                $"already been reached. Current attempt: " +
                $"'{currentAttempt}'.");
        }

        int delayIndex =
            currentAttempt - 1;

        return _delaySeconds[delayIndex];
    }

    private static void Validate(
        int maximumAttempts,
        int[] delaySeconds)
    {
        if (maximumAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumAttempts),
                maximumAttempts,
                "Effective RabbitMQ maximum attempts must be " +
                "at least one.");
        }

        if (delaySeconds.Any(
                static delay => delay <= 0))
        {
            throw new ArgumentException(
                "Every effective RabbitMQ retry delay must be " +
                "greater than zero.",
                nameof(delaySeconds));
        }

        if (delaySeconds
                .Distinct()
                .Count() !=
            delaySeconds.Length)
        {
            throw new ArgumentException(
                "Effective RabbitMQ retry delays must be unique.",
                nameof(delaySeconds));
        }

        if (delaySeconds.Length !=
            maximumAttempts - 1)
        {
            throw new ArgumentException(
                "Effective RabbitMQ retry delay count must equal " +
                "MaximumAttempts minus one.",
                nameof(delaySeconds));
        }
    }
}