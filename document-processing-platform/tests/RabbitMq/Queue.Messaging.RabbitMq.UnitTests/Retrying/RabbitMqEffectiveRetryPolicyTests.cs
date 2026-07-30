using Queue.Messaging.RabbitMq.Configuration;
using Queue.Messaging.RabbitMq.Retrying;
using Xunit;

namespace Queue.Messaging.RabbitMq.UnitTests.Retrying;

public sealed class RabbitMqEffectiveRetryPolicyTests
{
    [Fact]
    public void Resolve_uses_global_retry_policy_without_overrides()
    {
        RabbitMqRetryOptions globalOptions =
            CreateGlobalOptions();

        RabbitMqEffectiveRetryPolicy policy =
            RabbitMqEffectiveRetryPolicy.Resolve(
                globalOptions,
                maximumAttemptsOverride:
                    null,
                delaySecondsOverride:
                    null);

        Assert.Equal(
            4,
            policy.MaximumAttempts);

        Assert.Equal(
            new[] { 10, 60, 300 },
            policy.DelaySeconds);
    }

    [Fact]
    public void Resolve_uses_endpoint_retry_overrides()
    {
        RabbitMqRetryOptions globalOptions =
            CreateGlobalOptions();

        RabbitMqEffectiveRetryPolicy policy =
            RabbitMqEffectiveRetryPolicy.Resolve(
                globalOptions,
                maximumAttemptsOverride:
                    3,
                delaySecondsOverride:
                    new[] { 15, 120 });

        Assert.Equal(
            3,
            policy.MaximumAttempts);

        Assert.Equal(
            new[] { 15, 120 },
            policy.DelaySeconds);
    }

    [Fact]
    public void Resolve_supports_maximum_attempt_override_with_global_delays()
    {
        RabbitMqRetryOptions globalOptions =
            new()
            {
                MaximumAttempts =
                    3,

                DelaySeconds =
                    new[] { 15, 120 }
            };

        RabbitMqEffectiveRetryPolicy policy =
            RabbitMqEffectiveRetryPolicy.Resolve(
                globalOptions,
                maximumAttemptsOverride:
                    3,
                delaySecondsOverride:
                    null);

        Assert.Equal(
            3,
            policy.MaximumAttempts);

        Assert.Equal(
            new[] { 15, 120 },
            policy.DelaySeconds);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 60)]
    [InlineData(3, 300)]
    public void GetDelaySecondsForCurrentAttempt_returns_expected_delay(
        int currentAttempt,
        int expectedDelay)
    {
        RabbitMqEffectiveRetryPolicy policy =
            RabbitMqEffectiveRetryPolicy.Resolve(
                CreateGlobalOptions(),
                maximumAttemptsOverride:
                    null,
                delaySecondsOverride:
                    null);

        int actualDelay =
            policy.GetDelaySecondsForCurrentAttempt(
                currentAttempt);

        Assert.Equal(
            expectedDelay,
            actualDelay);
    }

    [Fact]
    public void GetDelaySecondsForCurrentAttempt_rejects_attempt_below_one()
    {
        RabbitMqEffectiveRetryPolicy policy =
            RabbitMqEffectiveRetryPolicy.Resolve(
                CreateGlobalOptions(),
                maximumAttemptsOverride:
                    null,
                delaySecondsOverride:
                    null);

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                policy.GetDelaySecondsForCurrentAttempt(
                    0));
    }

    [Fact]
    public void GetDelaySecondsForCurrentAttempt_rejects_terminal_attempt()
    {
        RabbitMqEffectiveRetryPolicy policy =
            RabbitMqEffectiveRetryPolicy.Resolve(
                CreateGlobalOptions(),
                maximumAttemptsOverride:
                    null,
                delaySecondsOverride:
                    null);

        Assert.Throws<InvalidOperationException>(
            () =>
                policy.GetDelaySecondsForCurrentAttempt(
                    4));
    }

    [Fact]
    public void Resolve_rejects_delay_count_mismatch()
    {
        RabbitMqRetryOptions globalOptions =
            CreateGlobalOptions();

        Assert.Throws<ArgumentException>(
            () =>
                RabbitMqEffectiveRetryPolicy.Resolve(
                    globalOptions,
                    maximumAttemptsOverride:
                        4,
                    delaySecondsOverride:
                        new[] { 10, 60 }));
    }

    [Fact]
    public void Resolve_rejects_duplicate_delays()
    {
        RabbitMqRetryOptions globalOptions =
            CreateGlobalOptions();

        Assert.Throws<ArgumentException>(
            () =>
                RabbitMqEffectiveRetryPolicy.Resolve(
                    globalOptions,
                    maximumAttemptsOverride:
                        3,
                    delaySecondsOverride:
                        new[] { 10, 10 }));
    }

    [Fact]
    public void Resolve_copies_retry_delay_array()
    {
        RabbitMqRetryOptions globalOptions =
            CreateGlobalOptions();

        int[] overrideDelays =
            new[] { 15, 120 };

        RabbitMqEffectiveRetryPolicy policy =
            RabbitMqEffectiveRetryPolicy.Resolve(
                globalOptions,
                maximumAttemptsOverride:
                    3,
                delaySecondsOverride:
                    overrideDelays);

        overrideDelays[0] =
            999;

        Assert.Equal(
            15,
            policy.DelaySeconds[0]);
    }

    private static RabbitMqRetryOptions
        CreateGlobalOptions()
    {
        return new RabbitMqRetryOptions
        {
            MaximumAttempts =
                4,

            DelaySeconds =
                new[] { 10, 60, 300 }
        };
    }
}