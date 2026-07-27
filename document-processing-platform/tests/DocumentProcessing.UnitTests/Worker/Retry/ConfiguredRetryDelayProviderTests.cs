using Queue.Messaging.RabbitMq.Configuration;
using DocumentProcessing.Worker.Consumers.Retry;
using Microsoft.Extensions.Options;
using Xunit;

namespace DocumentProcessing.UnitTests.Worker.Retry;

public sealed class ConfiguredRetryDelayProviderTests
{
    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 60)]
    [InlineData(3, 300)]
    public void TryGetNextDelay_WhenRetryExists_ReturnsConfiguredDelay(
        int currentAttempt,
        int expectedDelaySeconds)
    {
        ConfiguredRetryDelayProvider provider =
            CreateProvider();

        bool found =
            provider.TryGetNextDelay(
                currentAttempt,
                out TimeSpan delay);

        Assert.True(found);

        Assert.Equal(
            TimeSpan.FromSeconds(
                expectedDelaySeconds),
            delay);
    }

    [Fact]
    public void TryGetNextDelay_WhenMaximumAttemptReached_ReturnsFalse()
    {
        ConfiguredRetryDelayProvider provider =
            CreateProvider();

        bool found =
            provider.TryGetNextDelay(
                currentAttempt: 4,
                out TimeSpan delay);

        Assert.False(found);
        Assert.Equal(default, delay);
    }

    [Fact]
    public void TryGetNextDelay_WhenAttemptExceedsMaximum_ReturnsFalse()
    {
        ConfiguredRetryDelayProvider provider =
            CreateProvider();

        bool found =
            provider.TryGetNextDelay(
                currentAttempt: 5,
                out TimeSpan delay);

        Assert.False(found);
        Assert.Equal(default, delay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryGetNextDelay_WhenAttemptIsInvalid_Throws(
        int currentAttempt)
    {
        ConfiguredRetryDelayProvider provider =
            CreateProvider();

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    provider.TryGetNextDelay(
                        currentAttempt,
                        out _));

        Assert.Equal(
            "currentAttempt",
            exception.ParamName);
    }

    [Fact]
    public void MaximumAttempts_ReturnsConfiguredValue()
    {
        ConfiguredRetryDelayProvider provider =
            CreateProvider();

        Assert.Equal(
            4,
            provider.MaximumAttempts);
    }

    private static ConfiguredRetryDelayProvider
        CreateProvider()
    {
        RabbitMqRetryOptions options =
            new()
            {
                MaximumAttempts = 4,

                DelaySeconds =
                [
                    10,
                    60,
                    300
                ]
            };

        return new ConfiguredRetryDelayProvider(
            Options.Create(options));
    }
}