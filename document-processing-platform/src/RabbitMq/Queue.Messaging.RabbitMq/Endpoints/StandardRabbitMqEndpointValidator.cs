using Queue.Messaging.RabbitMq.Compatibility;

namespace Queue.Messaging.RabbitMq.Endpoints.Standard;

internal static class StandardRabbitMqEndpointValidator
{
    public static void Validate<TMessage>(
        StandardRabbitMqEndpointOptions options)
    {
        Guard.NotNull(
            options,
            nameof(options));

        Type messageType =
            typeof(TMessage);

        Require(
            options.EndpointName,
            nameof(options.EndpointName),
            messageType);

        Require(
            options.MessageType,
            nameof(options.MessageType),
            messageType);

        Require(
            options.MessageVersion,
            nameof(options.MessageVersion),
            messageType);

        StandardRabbitMqEndpointNames names =
            options.Names
            ?? throw new ArgumentException(
                "Standard RabbitMQ endpoint names are required.",
                nameof(options.Names));

        ValidateNames(
            names,
            messageType);

        ushort? prefetchCount =
            options.PrefetchCount;

        if (prefetchCount.HasValue &&
            prefetchCount.Value == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.PrefetchCount),
                prefetchCount.Value,
                "Endpoint prefetch count must be greater than zero.");
        }

        int? concurrentConsumerCount =
            options.ConcurrentConsumerCount;

        if (concurrentConsumerCount.HasValue &&
            concurrentConsumerCount.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.ConcurrentConsumerCount),
                concurrentConsumerCount.Value,
                "Endpoint concurrent consumer count must be greater than zero.");
        }

        TimeSpan? shutdownTimeout =
            options.ShutdownTimeout;

        if (shutdownTimeout.HasValue &&
            shutdownTimeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.ShutdownTimeout),
                shutdownTimeout.Value,
                "Endpoint shutdown timeout must be greater than zero.");
        }

        if (options.QueueType.HasValue &&
            !EnumCompatibility.IsDefined(
                options.QueueType.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.QueueType),
                options.QueueType.Value,
                "Endpoint queue type is invalid.");
        }

        int? maximumAttempts =
            options.MaximumAttempts;

        if (maximumAttempts.HasValue &&
            maximumAttempts.Value < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.MaximumAttempts),
                maximumAttempts.Value,
                "Endpoint maximum attempts must be at least one.");
        }

        /*
         * Nullable property değerini local değişkene alıyoruz.
         *
         * Property tekrar okunduğunda compiler değerin arada
         * değişmiş olabileceğini varsayabilir. Local değişken
         * nullable flow analizini kararlı hâle getirir.
         */
        int[]? delaySeconds =
            options.DelaySeconds;

        if (delaySeconds is not null)
        {
            ValidateDelaySeconds(
                delaySeconds);
        }

        /*
         * Hem MaximumAttempts hem DelaySeconds endpoint
         * üzerinde verilmişse burada doğrudan doğrulanır.
         *
         * Yalnızca biri verilmişse diğer değer global
         * RabbitMqRetryOptions üzerinden alınacaktır.
         * Effective doğrulama registration aşamasında yapılacak.
         */
        if (maximumAttempts.HasValue &&
            delaySeconds is not null &&
            delaySeconds.Length !=
            maximumAttempts.Value - 1)
        {
            throw new ArgumentException(
                "Endpoint retry delay count must equal " +
                "MaximumAttempts minus one.",
                nameof(options));
        }
    }

    private static void ValidateNames(
        StandardRabbitMqEndpointNames names,
        Type messageType)
    {
        Require(
            names.ExchangeName,
            nameof(names.ExchangeName),
            messageType);

        Require(
            names.QueueName,
            nameof(names.QueueName),
            messageType);

        Require(
            names.RoutingKey,
            nameof(names.RoutingKey),
            messageType);

        Require(
            names.RetryExchangeName,
            nameof(names.RetryExchangeName),
            messageType);

        Require(
            names.RetryQueueNamePrefix,
            nameof(names.RetryQueueNamePrefix),
            messageType);

        Require(
            names.RetryRoutingKeyPrefix,
            nameof(names.RetryRoutingKeyPrefix),
            messageType);

        Require(
            names.DeadLetterExchangeName,
            nameof(names.DeadLetterExchangeName),
            messageType);

        Require(
            names.DeadLetterQueueName,
            nameof(names.DeadLetterQueueName),
            messageType);

        Require(
            names.DeadLetterRoutingKey,
            nameof(names.DeadLetterRoutingKey),
            messageType);

        Require(
            names.ConsumerTagPrefix,
            nameof(names.ConsumerTagPrefix),
            messageType);
    }

    private static void ValidateDelaySeconds(
        int[] delaySeconds)
    {
        if (delaySeconds.Any(
                static delay => delay <= 0))
        {
            throw new ArgumentException(
                "Every endpoint retry delay must be greater than zero.",
                nameof(delaySeconds));
        }

        if (delaySeconds
                .Distinct()
                .Count() !=
            delaySeconds.Length)
        {
            throw new ArgumentException(
                "Endpoint retry delays must be unique.",
                nameof(delaySeconds));
        }
    }

    private static void Require(
        string? value,
        string propertyName,
        Type messageType)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        throw new ArgumentException(
            "Standard RabbitMQ endpoint property " +
            $"'{propertyName}' is required for message type " +
            $"'{messageType.FullName}'.",
            propertyName);
    }
}