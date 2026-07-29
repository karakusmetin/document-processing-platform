using Queue.Messaging.RabbitMq.Compatibility;
using Queue.Messaging.RabbitMq.Configuration;

namespace Queue.Messaging.RabbitMq.Consuming;

/// <summary>
/// Global consumer configuration ile endpoint bazlı
/// override değerlerinin birleştirilmiş hâlidir.
///
/// Consumer runtime yalnızca bu sınıfı kullanır.
/// </summary>
internal sealed class RabbitMqEffectiveConsumerOptions
{
    private RabbitMqEffectiveConsumerOptions(
        ushort prefetchCount,
        int concurrentConsumerCount,
        TimeSpan shutdownTimeout)
    {
        PrefetchCount =
            prefetchCount;

        ConcurrentConsumerCount =
            concurrentConsumerCount;

        ShutdownTimeout =
            shutdownTimeout;
    }

    public ushort PrefetchCount { get; }

    public int ConcurrentConsumerCount { get; }

    public TimeSpan ShutdownTimeout { get; }

    public static RabbitMqEffectiveConsumerOptions Resolve<TMessage>(
        RabbitMqConsumerOptions globalOptions,
        RabbitMqConsumerDefinition<TMessage> definition)
    {
        Guard.NotNull(
            globalOptions,
            nameof(globalOptions));

        Guard.NotNull(
            definition,
            nameof(definition));

        ushort prefetchCount =
            definition.PrefetchCount
            ?? globalOptions.PrefetchCount;

        int concurrentConsumerCount =
            definition.ConcurrentConsumerCount
            ?? globalOptions.ConcurrentConsumerCount;

        TimeSpan shutdownTimeout =
            definition.ShutdownTimeout
            ?? globalOptions.ShutdownTimeout;

        Validate(
            prefetchCount,
            concurrentConsumerCount,
            shutdownTimeout);

        return new RabbitMqEffectiveConsumerOptions(
            prefetchCount,
            concurrentConsumerCount,
            shutdownTimeout);
    }

    private static void Validate(
        ushort prefetchCount,
        int concurrentConsumerCount,
        TimeSpan shutdownTimeout)
    {
        if (prefetchCount == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(prefetchCount),
                prefetchCount,
                "Effective RabbitMQ consumer prefetch count " +
                "must be greater than zero.");
        }

        if (concurrentConsumerCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(concurrentConsumerCount),
                concurrentConsumerCount,
                "Effective RabbitMQ concurrent consumer count " +
                "must be greater than zero.");
        }

        if (shutdownTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(shutdownTimeout),
                shutdownTimeout,
                "Effective RabbitMQ consumer shutdown timeout " +
                "must be greater than zero.");
        }
    }
}