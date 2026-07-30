using Queue.Messaging.RabbitMq.Configuration;
using Queue.Messaging.RabbitMq.Consuming;
using Queue.Messaging.RabbitMq.UnitTests.TestDoubles;
using Xunit;

namespace Queue.Messaging.RabbitMq.UnitTests.Consuming;

public sealed class RabbitMqEffectiveConsumerOptionsTests
{
    [Fact]
    public void Resolve_uses_global_values_when_endpoint_has_no_overrides()
    {
        RabbitMqConsumerOptions globalOptions =
            CreateGlobalOptions();

        RabbitMqConsumerDefinition<TestMessage> definition =
            new();

        RabbitMqEffectiveConsumerOptions effectiveOptions =
            RabbitMqEffectiveConsumerOptions.Resolve(
                globalOptions,
                definition);

        Assert.Equal(
            globalOptions.PrefetchCount,
            effectiveOptions.PrefetchCount);

        Assert.Equal(
            globalOptions.ConcurrentConsumerCount,
            effectiveOptions.ConcurrentConsumerCount);

        Assert.Equal(
            globalOptions.ShutdownTimeout,
            effectiveOptions.ShutdownTimeout);
    }

    [Fact]
    public void Resolve_uses_endpoint_overrides()
    {
        RabbitMqConsumerOptions globalOptions =
            CreateGlobalOptions();

        RabbitMqConsumerDefinition<TestMessage> definition =
            new()
            {
                PrefetchCount =
                    5,

                ConcurrentConsumerCount =
                    7,

                ShutdownTimeout =
                    TimeSpan.FromSeconds(90)
            };

        RabbitMqEffectiveConsumerOptions effectiveOptions =
            RabbitMqEffectiveConsumerOptions.Resolve(
                globalOptions,
                definition);

        Assert.Equal(
            (ushort)5,
            effectiveOptions.PrefetchCount);

        Assert.Equal(
            7,
            effectiveOptions.ConcurrentConsumerCount);

        Assert.Equal(
            TimeSpan.FromSeconds(90),
            effectiveOptions.ShutdownTimeout);
    }

    [Fact]
    public void Resolve_supports_partial_overrides()
    {
        RabbitMqConsumerOptions globalOptions =
            CreateGlobalOptions();

        RabbitMqConsumerDefinition<TestMessage> definition =
            new()
            {
                ConcurrentConsumerCount =
                    8
            };

        RabbitMqEffectiveConsumerOptions effectiveOptions =
            RabbitMqEffectiveConsumerOptions.Resolve(
                globalOptions,
                definition);

        Assert.Equal(
            globalOptions.PrefetchCount,
            effectiveOptions.PrefetchCount);

        Assert.Equal(
            8,
            effectiveOptions.ConcurrentConsumerCount);

        Assert.Equal(
            globalOptions.ShutdownTimeout,
            effectiveOptions.ShutdownTimeout);
    }

    [Fact]
    public void Resolve_rejects_zero_effective_prefetch()
    {
        RabbitMqConsumerOptions globalOptions =
            CreateGlobalOptions();

        globalOptions.PrefetchCount =
            0;

        RabbitMqConsumerDefinition<TestMessage> definition =
            new();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                RabbitMqEffectiveConsumerOptions.Resolve(
                    globalOptions,
                    definition));
    }

    [Fact]
    public void Resolve_rejects_invalid_effective_consumer_count()
    {
        RabbitMqConsumerOptions globalOptions =
            CreateGlobalOptions();

        globalOptions.ConcurrentConsumerCount =
            0;

        RabbitMqConsumerDefinition<TestMessage> definition =
            new();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                RabbitMqEffectiveConsumerOptions.Resolve(
                    globalOptions,
                    definition));
    }

    [Fact]
    public void Resolve_rejects_invalid_effective_shutdown_timeout()
    {
        RabbitMqConsumerOptions globalOptions =
            CreateGlobalOptions();

        globalOptions.ShutdownTimeout =
            TimeSpan.Zero;

        RabbitMqConsumerDefinition<TestMessage> definition =
            new();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                RabbitMqEffectiveConsumerOptions.Resolve(
                    globalOptions,
                    definition));
    }

    private static RabbitMqConsumerOptions
        CreateGlobalOptions()
    {
        return new RabbitMqConsumerOptions
        {
            PrefetchCount =
                2,

            ConcurrentConsumerCount =
                3,

            ShutdownTimeout =
                TimeSpan.FromSeconds(30)
        };
    }
}