using Queue.Messaging.RabbitMq.Publishing;
using Xunit;

namespace DocumentProcessing.UnitTests.Messaging.Publishing;

public sealed class RabbitMqMessageRouteResolverTests
{
    [Fact]
    public void Resolve_WhenRouteIsRegistered_ReturnsRegisteredRoute()
    {
        RabbitMqMessageRoute expectedRoute =
            new(
                Exchange:
                    "test.commands",

                RoutingKey:
                    "test.message-requested",

                MessageType:
                    "test.message-requested",

                MessageVersion:
                    "1.0",

                RetryExchange:
                    "test.retry",

                RetryRoutingKeyPrefix:
                    "test.message.retry");

        IRabbitMqMessageRouteRegistration registration =
            new RabbitMqMessageRouteRegistration<TestMessage>(
                expectedRoute);

        RabbitMqMessageRouteResolver resolver =
            new(
                [
                    registration
                ]);

        RabbitMqMessageRoute actualRoute =
            resolver.Resolve<TestMessage>();

        Assert.Same(
            expectedRoute,
            actualRoute);

        Assert.Equal(
            "test.commands",
            actualRoute.Exchange);

        Assert.Equal(
            "test.message-requested",
            actualRoute.RoutingKey);

        Assert.Equal(
            "test.message-requested",
            actualRoute.MessageType);

        Assert.Equal(
            "1.0",
            actualRoute.MessageVersion);

        Assert.Equal(
            "test.retry",
            actualRoute.RetryExchange);

        Assert.Equal(
            "test.message.retry",
            actualRoute.RetryRoutingKeyPrefix);
    }

    [Fact]
    public void Resolve_WhenRouteIsNotRegistered_Throws()
    {
        RabbitMqMessageRouteResolver resolver =
            new(
                Array.Empty<
                    IRabbitMqMessageRouteRegistration>());

        NotSupportedException exception =
            Assert.Throws<NotSupportedException>(
                () =>
                    resolver.Resolve<TestMessage>());

        Assert.Contains(
            typeof(TestMessage).FullName!,
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WhenSameMessageTypeRegisteredTwice_Throws()
    {
        RabbitMqMessageRoute firstRoute =
            CreateRoute(
                exchange:
                    "first.commands");

        RabbitMqMessageRoute secondRoute =
            CreateRoute(
                exchange:
                    "second.commands");

        IRabbitMqMessageRouteRegistration firstRegistration =
            new RabbitMqMessageRouteRegistration<TestMessage>(
                firstRoute);

        IRabbitMqMessageRouteRegistration secondRegistration =
            new RabbitMqMessageRouteRegistration<TestMessage>(
                secondRoute);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    new RabbitMqMessageRouteResolver(
                        [
                            firstRegistration,
                            secondRegistration
                        ]));

        Assert.Contains(
            typeof(TestMessage).FullName!,
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_WhenDifferentMessageTypesRegistered_ReturnsCorrectRoute()
    {
        RabbitMqMessageRoute firstRoute =
            CreateRoute(
                exchange:
                    "first.commands");

        RabbitMqMessageRoute secondRoute =
            new(
                Exchange:
                    "second.events",

                RoutingKey:
                    "second.completed",

                MessageType:
                    "second.completed",

                MessageVersion:
                    "2.0",

                RetryExchange:
                    null,

                RetryRoutingKeyPrefix:
                    null);

        RabbitMqMessageRouteResolver resolver =
            new(
                [
                    new RabbitMqMessageRouteRegistration<TestMessage>(
                        firstRoute),

                    new RabbitMqMessageRouteRegistration<
                        SecondTestMessage>(
                        secondRoute)
                ]);

        RabbitMqMessageRoute resolvedFirst =
            resolver.Resolve<TestMessage>();

        RabbitMqMessageRoute resolvedSecond =
            resolver.Resolve<SecondTestMessage>();

        Assert.Same(
            firstRoute,
            resolvedFirst);

        Assert.Same(
            secondRoute,
            resolvedSecond);
    }

    private static RabbitMqMessageRoute CreateRoute(
        string exchange)
    {
        return new RabbitMqMessageRoute(
            Exchange:
                exchange,

            RoutingKey:
                "test.requested",

            MessageType:
                "test.requested",

            MessageVersion:
                "1.0",

            RetryExchange:
                "test.retry",

            RetryRoutingKeyPrefix:
                "test.retry.request");
    }

    private sealed record TestMessage;

    private sealed record SecondTestMessage;
}