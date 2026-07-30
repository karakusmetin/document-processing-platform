using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Queue.Messaging.RabbitMq.Consuming;
using Queue.Messaging.RabbitMq.Endpoints.Standard;
using Queue.Messaging.RabbitMq.Publishing;
using Queue.Messaging.RabbitMq.Topology;
using Queue.Messaging.RabbitMq.UnitTests.TestDoubles;
using Xunit;

namespace Queue.Messaging.RabbitMq.UnitTests.DependencyInjection;

public sealed class
    StandardRabbitMqEndpointServiceCollectionExtensionsTests
{
    [Fact]
    public void AddStandardRabbitMqEndpoint_registers_endpoint_snapshot()
    {
        ServiceCollection services =
            new();

        services.AddStandardRabbitMqEndpoint<
            TestMessage,
            TestMessageHandler>(
                "test-endpoint",
                options =>
                {
                    options.MessageType =
                        "test.message";

                    options.MessageVersion =
                        "2.0";

                    options.PrefetchCount =
                        5;

                    options.ConcurrentConsumerCount =
                        3;

                    options.MaximumAttempts =
                        3;

                    options.DelaySeconds =
                        new[] { 15, 120 };
                });

        ServiceDescriptor descriptor =
            Assert.Single(
                services,
                item =>
                    item.ServiceType ==
                    typeof(
                        StandardRabbitMqEndpointRegistration<
                            TestMessage>));

        StandardRabbitMqEndpointRegistration<TestMessage>
            registration =
                Assert.IsType<
                    StandardRabbitMqEndpointRegistration<
                        TestMessage>>(
                    descriptor.ImplementationInstance);

        Assert.Equal(
            "test-endpoint",
            registration.EndpointName);

        Assert.Equal(
            "test.message",
            registration.MessageType);

        Assert.Equal(
            "2.0",
            registration.MessageVersion);

        Assert.Equal(
            (ushort)5,
            registration.PrefetchCount);

        Assert.Equal(
            3,
            registration.ConcurrentConsumerCount);

        Assert.Equal(
            3,
            registration.MaximumAttempts);

        Assert.Equal(
            new[] { 15, 120 },
            registration.DelaySeconds);
    }

    [Fact]
    public void AddStandardRabbitMqEndpoint_registers_consumer_options()
    {
        ServiceCollection services =
            new();

        services.AddStandardRabbitMqEndpoint<
            TestMessage,
            TestMessageHandler>(
                "test-endpoint",
                options =>
                {
                    options.MessageType =
                        "test.message";

                    options.PrefetchCount =
                        4;

                    options.ConcurrentConsumerCount =
                        6;

                    options.ShutdownTimeout =
                        TimeSpan.FromSeconds(75);
                });

        using ServiceProvider provider =
            services.BuildServiceProvider();

        RabbitMqConsumerDefinition<TestMessage> definition =
            provider
                .GetRequiredService<
                    IOptions<
                        RabbitMqConsumerDefinition<
                            TestMessage>>>()
                .Value;

        Assert.Equal(
            "test-endpoint.queue",
            definition.QueueName);

        Assert.Equal(
            "test.message",
            definition.MessageType);

        Assert.Equal(
            "1.0",
            definition.MessageVersion);

        Assert.Equal(
            "test-endpoint",
            definition.ConsumerTagPrefix);

        Assert.Equal(
            (ushort)4,
            definition.PrefetchCount);

        Assert.Equal(
            6,
            definition.ConcurrentConsumerCount);

        Assert.Equal(
            TimeSpan.FromSeconds(75),
            definition.ShutdownTimeout);
    }

    [Fact]
    public void AddStandardRabbitMqEndpoint_registers_route_consumer_and_topology()
    {
        ServiceCollection services =
            new();

        services.AddStandardRabbitMqEndpoint<
            TestMessage,
            TestMessageHandler>(
                "test-endpoint");

        Assert.Single(
            services,
            item =>
                item.ServiceType ==
                typeof(
                    IRabbitMqMessageRouteRegistration));
        Assert.Single(
            services,
            item =>
                item.ServiceType ==
                typeof(
                    IRabbitMqTopologyDefinition));

        Assert.Contains(
            services,
            item =>
                item.ServiceType ==
                typeof(
                    IRabbitMqMessageHandler<TestMessage>));

        Assert.Contains(
            services,
            item =>
                item.ServiceType ==
                typeof(IHostedService));
    }

    [Fact]
    public void AddStandardRabbitMqEndpoint_rejects_duplicate_endpoint_name()
    {
        ServiceCollection services =
            new();

        services.AddStandardRabbitMqEndpoint<
            TestMessage,
            TestMessageHandler>(
                "duplicate-endpoint");

        Assert.Throws<InvalidOperationException>(
            () =>
                services.AddStandardRabbitMqEndpoint<
                    SecondTestMessage,
                    SecondTestMessageHandler>(
                        "duplicate-endpoint"));
    }

    [Fact]
    public void AddStandardRabbitMqEndpoint_rejects_duplicate_message_type()
    {
        ServiceCollection services =
            new();

        services.AddStandardRabbitMqEndpoint<
            TestMessage,
            TestMessageHandler>(
                "first-endpoint");

        Assert.Throws<InvalidOperationException>(
            () =>
                services.AddStandardRabbitMqEndpoint<
                    TestMessage,
                    TestMessageHandler>(
                        "second-endpoint"));
    }

    [Fact]
    public void AddStandardRabbitMqEndpoint_supports_multiple_distinct_endpoints()
    {
        ServiceCollection services =
            new();

        services.AddStandardRabbitMqEndpoint<
            TestMessage,
            TestMessageHandler>(
                "first-endpoint");

        services.AddStandardRabbitMqEndpoint<
            SecondTestMessage,
            SecondTestMessageHandler>(
                "second-endpoint");

        Assert.Equal(
            2,
            services.Count(
                item =>
                    item.ServiceType ==
                    typeof(
                        IRabbitMqMessageRouteRegistration)));

        Assert.Equal(
            2,
            services.Count(
                item =>
                    item.ServiceType ==
                    typeof(
                        IRabbitMqTopologyDefinition)));

        int topologyHostedServiceCount =
            services.Count(
                item =>
                    item.ServiceType ==
                        typeof(IHostedService) &&
                    string.Equals(
                        item.ImplementationType?.Name,
                        "RabbitMqTopologyHostedService",
                        StringComparison.Ordinal));

        Assert.Equal(
            1,
            topologyHostedServiceCount);
    }
}