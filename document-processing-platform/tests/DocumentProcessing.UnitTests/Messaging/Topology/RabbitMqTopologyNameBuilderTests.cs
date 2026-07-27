using Queue.Messaging.RabbitMq.Topology;
using Xunit;

namespace DocumentProcessing.UnitTests.Messaging.Topology;

public sealed class RabbitMqTopologyNameBuilderTests
{
    [Theory]
    [InlineData(
        "document-processing.conversion.retry",
        10,
        "document-processing.conversion.retry.10s")]
    [InlineData(
        "document-processing.conversion.retry",
        60,
        "document-processing.conversion.retry.60s")]
    [InlineData(
        "document-processing.conversion.retry",
        300,
        "document-processing.conversion.retry.300s")]
    public void GetRetryQueueName_WhenValuesAreValid_ReturnsExpectedName(
        string prefix,
        int delaySeconds,
        string expected)
    {
        string actual =
            RabbitMqTopologyNameBuilder
                .GetRetryQueueName(
                    prefix,
                    delaySeconds);

        Assert.Equal(
            expected,
            actual);
    }

    [Theory]
    [InlineData(
        "document-processing.conversion.retry",
        10,
        "document-processing.conversion.retry.10s")]
    [InlineData(
        "document-processing.conversion.retry",
        60,
        "document-processing.conversion.retry.60s")]
    [InlineData(
        "document-processing.conversion.retry",
        300,
        "document-processing.conversion.retry.300s")]
    public void GetRetryRoutingKey_WhenValuesAreValid_ReturnsExpectedKey(
        string prefix,
        int delaySeconds,
        string expected)
    {
        string actual =
            RabbitMqTopologyNameBuilder
                .GetRetryRoutingKey(
                    prefix,
                    delaySeconds);

        Assert.Equal(
            expected,
            actual);
    }
}