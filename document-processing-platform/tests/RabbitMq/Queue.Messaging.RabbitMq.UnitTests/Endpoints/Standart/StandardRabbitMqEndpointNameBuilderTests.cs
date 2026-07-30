using Queue.Messaging.RabbitMq.Endpoints.Standard;
using Xunit;

namespace Queue.Messaging.RabbitMq.UnitTests.Endpoints.Standard;

public sealed class StandardRabbitMqEndpointNameBuilderTests
{
    [Fact]
    public void Build_creates_all_names_from_endpoint_name()
    {
        StandardRabbitMqEndpointNames names =
            StandardRabbitMqEndpointNameBuilder.Build(
                "document-conversion");

        Assert.Equal(
            "document-conversion.exchange",
            names.ExchangeName);

        Assert.Equal(
            "document-conversion.queue",
            names.QueueName);

        Assert.Equal(
            "document-conversion",
            names.RoutingKey);

        Assert.Equal(
            "document-conversion.retry.exchange",
            names.RetryExchangeName);

        Assert.Equal(
            "document-conversion.retry.queue",
            names.RetryQueueNamePrefix);

        Assert.Equal(
            "document-conversion.retry",
            names.RetryRoutingKeyPrefix);

        Assert.Equal(
            "document-conversion.dead-letter.exchange",
            names.DeadLetterExchangeName);

        Assert.Equal(
            "document-conversion.dead-letter.queue",
            names.DeadLetterQueueName);

        Assert.Equal(
            "document-conversion.dead-letter",
            names.DeadLetterRoutingKey);

        Assert.Equal(
            "document-conversion",
            names.ConsumerTagPrefix);
    }

    [Fact]
    public void Build_trims_endpoint_name()
    {
        StandardRabbitMqEndpointNames names =
            StandardRabbitMqEndpointNameBuilder.Build(
                "  document-conversion  ");

        Assert.Equal(
            "document-conversion.exchange",
            names.ExchangeName);

        Assert.Equal(
            "document-conversion.queue",
            names.QueueName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Build_rejects_blank_endpoint_name(
        string endpointName)
    {
        Assert.ThrowsAny<ArgumentException>(
            () =>
                StandardRabbitMqEndpointNameBuilder.Build(
                    endpointName));
    }

    [Fact]
    public void Build_rejects_null_endpoint_name()
    {
        Assert.ThrowsAny<ArgumentException>(
            () =>
                StandardRabbitMqEndpointNameBuilder.Build(
                    null!));
    }
}