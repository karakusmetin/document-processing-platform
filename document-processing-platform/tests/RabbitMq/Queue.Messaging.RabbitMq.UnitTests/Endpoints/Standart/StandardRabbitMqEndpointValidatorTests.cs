using Queue.Messaging.RabbitMq.Configuration;
using Queue.Messaging.RabbitMq.Endpoints.Standard;
using Queue.Messaging.RabbitMq.UnitTests.TestDoubles;
using Xunit;

namespace Queue.Messaging.RabbitMq.UnitTests.Endpoints.Standard;

public sealed class StandardRabbitMqEndpointValidatorTests
{
    [Fact]
    public void Validate_accepts_valid_endpoint()
    {
        StandardRabbitMqEndpointOptions options =
            CreateValidOptions();

        Exception? exception =
            Record.Exception(
                () =>
                    StandardRabbitMqEndpointValidator
                        .Validate<TestMessage>(
                            options));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_rejects_blank_message_type()
    {
        StandardRabbitMqEndpointOptions options =
            CreateValidOptions();

        options.MessageType =
            " ";

        Assert.Throws<ArgumentException>(
            () =>
                StandardRabbitMqEndpointValidator
                    .Validate<TestMessage>(
                        options));
    }

    [Fact]
    public void Validate_rejects_blank_queue_name()
    {
        StandardRabbitMqEndpointOptions options =
            CreateValidOptions();

        options.Names.QueueName =
            string.Empty;

        Assert.Throws<ArgumentException>(
            () =>
                StandardRabbitMqEndpointValidator
                    .Validate<TestMessage>(
                        options));
    }

    [Fact]
    public void Validate_rejects_zero_prefetch()
    {
        StandardRabbitMqEndpointOptions options =
            CreateValidOptions();

        options.PrefetchCount =
            0;

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                StandardRabbitMqEndpointValidator
                    .Validate<TestMessage>(
                        options));
    }

    [Fact]
    public void Validate_rejects_invalid_consumer_count()
    {
        StandardRabbitMqEndpointOptions options =
            CreateValidOptions();

        options.ConcurrentConsumerCount =
            0;

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                StandardRabbitMqEndpointValidator
                    .Validate<TestMessage>(
                        options));
    }

    [Fact]
    public void Validate_rejects_invalid_shutdown_timeout()
    {
        StandardRabbitMqEndpointOptions options =
            CreateValidOptions();

        options.ShutdownTimeout =
            TimeSpan.Zero;

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                StandardRabbitMqEndpointValidator
                    .Validate<TestMessage>(
                        options));
    }

    [Fact]
    public void Validate_rejects_maximum_attempts_below_one()
    {
        StandardRabbitMqEndpointOptions options =
            CreateValidOptions();

        options.MaximumAttempts =
            0;

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                StandardRabbitMqEndpointValidator
                    .Validate<TestMessage>(
                        options));
    }

    [Fact]
    public void Validate_rejects_non_positive_retry_delay()
    {
        StandardRabbitMqEndpointOptions options =
            CreateValidOptions();

        options.MaximumAttempts =
            3;

        options.DelaySeconds =
            new[] { 10, 0 };

        Assert.Throws<ArgumentException>(
            () =>
                StandardRabbitMqEndpointValidator
                    .Validate<TestMessage>(
                        options));
    }

    [Fact]
    public void Validate_rejects_duplicate_retry_delays()
    {
        StandardRabbitMqEndpointOptions options =
            CreateValidOptions();

        options.MaximumAttempts =
            3;

        options.DelaySeconds =
            new[] { 10, 10 };

        Assert.Throws<ArgumentException>(
            () =>
                StandardRabbitMqEndpointValidator
                    .Validate<TestMessage>(
                        options));
    }

    [Fact]
    public void Validate_rejects_retry_delay_count_mismatch()
    {
        StandardRabbitMqEndpointOptions options =
            CreateValidOptions();

        options.MaximumAttempts =
            4;

        options.DelaySeconds =
            new[] { 10, 60 };

        Assert.Throws<ArgumentException>(
            () =>
                StandardRabbitMqEndpointValidator
                    .Validate<TestMessage>(
                        options));
    }

    [Fact]
    public void Validate_accepts_queue_type_override()
    {
        StandardRabbitMqEndpointOptions options =
            CreateValidOptions();

        options.QueueType =
            RabbitMqQueueType.Classic;

        Exception? exception =
            Record.Exception(
                () =>
                    StandardRabbitMqEndpointValidator
                        .Validate<TestMessage>(
                            options));

        Assert.Null(exception);
    }

    private static StandardRabbitMqEndpointOptions
        CreateValidOptions()
    {
        return new StandardRabbitMqEndpointOptions(
            "test-endpoint")
        {
            MessageType =
                "test.message",

            MessageVersion =
                "1.0",

            PrefetchCount =
                2,

            ConcurrentConsumerCount =
                3,

            ShutdownTimeout =
                TimeSpan.FromSeconds(30),

            MaximumAttempts =
                4,

            DelaySeconds =
                new[] { 10, 60, 300 }
        };
    }
}