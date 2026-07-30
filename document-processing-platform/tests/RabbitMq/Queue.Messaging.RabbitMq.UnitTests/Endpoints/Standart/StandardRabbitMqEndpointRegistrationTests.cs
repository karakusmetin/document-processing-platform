using Queue.Messaging.RabbitMq.Configuration;
using Queue.Messaging.RabbitMq.Endpoints.Standard;
using Queue.Messaging.RabbitMq.UnitTests.TestDoubles;
using Xunit;

namespace Queue.Messaging.RabbitMq.UnitTests.Endpoints.Standard;

public sealed class StandardRabbitMqEndpointRegistrationTests
{
    [Fact]
    public void Constructor_copies_all_endpoint_values()
    {
        StandardRabbitMqEndpointOptions options =
            CreateOptions();

        StandardRabbitMqEndpointRegistration<TestMessage>
            registration =
                new(options);

        Assert.Equal(
            "test-endpoint",
            registration.EndpointName);

        Assert.Equal(
            "test.message",
            registration.MessageType);

        Assert.Equal(
            "1.0",
            registration.MessageVersion);

        Assert.Equal(
            15,
            registration.TopologyOrder);

        Assert.Equal(
            (ushort)2,
            registration.PrefetchCount);

        Assert.Equal(
            4,
            registration.ConcurrentConsumerCount);

        Assert.Equal(
            TimeSpan.FromSeconds(45),
            registration.ShutdownTimeout);

        Assert.Equal(
            RabbitMqQueueType.Classic,
            registration.QueueType);

        Assert.Equal(
            4,
            registration.MaximumAttempts);

        Assert.Equal(
            new[] { 10, 60, 300 },
            registration.DelaySeconds);
    }

    [Fact]
    public void Constructor_copies_mutable_names_and_delay_array()
    {
        StandardRabbitMqEndpointOptions options =
            CreateOptions();

        int[] originalDelays =
            options.DelaySeconds!;

        StandardRabbitMqEndpointNames originalNames =
            options.Names;

        StandardRabbitMqEndpointRegistration<TestMessage>
            registration =
                new(options);

        originalDelays[0] =
            999;

        originalNames.QueueName =
            "changed.queue";

        Assert.Equal(
            10,
            registration.DelaySeconds![0]);

        Assert.Equal(
            "test-endpoint.queue",
            registration.Names.QueueName);

        Assert.NotSame(
            originalDelays,
            registration.DelaySeconds);

        Assert.NotSame(
            originalNames,
            registration.Names);
    }

    private static StandardRabbitMqEndpointOptions
        CreateOptions()
    {
        return new StandardRabbitMqEndpointOptions(
            "test-endpoint")
        {
            MessageType =
                "test.message",

            MessageVersion =
                "1.0",

            TopologyOrder =
                15,

            PrefetchCount =
                2,

            ConcurrentConsumerCount =
                4,

            ShutdownTimeout =
                TimeSpan.FromSeconds(45),

            QueueType =
                RabbitMqQueueType.Classic,

            MaximumAttempts =
                4,

            DelaySeconds =
                new[] { 10, 60, 300 }
        };
    }
}