using DocumentProcessing.Messaging.RabbitMq.DependencyInjection;
using DocumentProcessing.Messaging.RabbitMq.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DocumentProcessing.UnitTests.Messaging.DependencyInjection;

public sealed class RabbitMqMessageRouteRegistrationTests
{
    [Fact]
    public void AddRabbitMqMessageRoute_WhenDefinitionIsValid_RegistersRoute()
    {
        ServiceCollection services =
            new();

        services.AddRabbitMqMessageRoute<TestMessage>(
            route =>
            {
                route.Exchange =
                    "test.commands";

                route.RoutingKey =
                    "test.requested";

                route.MessageType =
                    "test.requested";

                route.MessageVersion =
                    "1.0";

                route.RetryExchange =
                    "test.retry";

                route.RetryRoutingKeyPrefix =
                    "test.request.retry";
            });

        using ServiceProvider provider =
            services.BuildServiceProvider();

        IEnumerable<
            IRabbitMqMessageRouteRegistration> registrations =
            provider.GetServices<
                IRabbitMqMessageRouteRegistration>();

        Assert.Single(registrations);
    }

    [Fact]
    public void AddRabbitMqMessageRoute_WhenExchangeMissing_Throws()
    {
        ServiceCollection services =
            new();

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    services.AddRabbitMqMessageRoute<TestMessage>(
                        route =>
                        {
                            route.RoutingKey =
                                "test.requested";

                            route.MessageType =
                                "test.requested";

                            route.MessageVersion =
                                "1.0";
                        }));

        Assert.Contains(
            "exchange",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddRabbitMqMessageRoute_WhenOnlyRetryExchangeProvided_Throws()
    {
        ServiceCollection services =
            new();

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    services.AddRabbitMqMessageRoute<TestMessage>(
                        route =>
                        {
                            route.Exchange =
                                "test.commands";

                            route.RoutingKey =
                                "test.requested";

                            route.MessageType =
                                "test.requested";

                            route.MessageVersion =
                                "1.0";

                            route.RetryExchange =
                                "test.retry";
                        }));

        Assert.Contains(
            "retry",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddRabbitMqMessageRoute_WhenOnlyRetryPrefixProvided_Throws()
    {
        ServiceCollection services =
            new();

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    services.AddRabbitMqMessageRoute<TestMessage>(
                        route =>
                        {
                            route.Exchange =
                                "test.commands";

                            route.RoutingKey =
                                "test.requested";

                            route.MessageType =
                                "test.requested";

                            route.MessageVersion =
                                "1.0";

                            route.RetryRoutingKeyPrefix =
                                "test.request.retry";
                        }));

        Assert.Contains(
            "retry",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed record TestMessage;
}