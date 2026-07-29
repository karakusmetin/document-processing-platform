using Microsoft.Extensions.DependencyInjection;
using Queue.Messaging.RabbitMq.Compatibility;
using Queue.Messaging.RabbitMq.Publishing;

namespace Queue.Messaging.RabbitMq.DependencyInjection;

public static class
    RabbitMqMessageRouteServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqMessageRoute<
        TMessage>(
        this IServiceCollection services,
        Action<RabbitMqMessageRouteDefinition<TMessage>>
            configure)
    {
        Guard.NotNull(
            services,
            nameof(services));

        Guard.NotNull(
            configure,
            nameof(configure));

        RabbitMqMessageRouteDefinition<TMessage> definition =
            new();

        configure(definition);

        ValidateDefinition(
            definition);

        int[]? retryDelaySeconds =
            definition.RetryDelaySeconds?.ToArray();

        RabbitMqMessageRoute route =
            new(
                Exchange:
                    definition.Exchange,

                RoutingKey:
                    definition.RoutingKey,

                MessageType:
                    definition.MessageType,

                MessageVersion:
                    definition.MessageVersion,

                RetryExchange:
                    NullWhenWhiteSpace(
                        definition.RetryExchange),

                RetryRoutingKeyPrefix:
                    NullWhenWhiteSpace(
                        definition.RetryRoutingKeyPrefix),

                RetryMaximumAttempts:
                    definition.RetryMaximumAttempts,

                RetryDelaySeconds:
                    retryDelaySeconds);

        services.AddSingleton<
            IRabbitMqMessageRouteRegistration>(
            new RabbitMqMessageRouteRegistration<TMessage>(
                route));

        return services;
    }

    private static void ValidateDefinition<TMessage>(
        RabbitMqMessageRouteDefinition<TMessage> definition)
    {
        if (string.IsNullOrWhiteSpace(
                definition.Exchange))
        {
            throw new ArgumentException(
                "RabbitMQ exchange is required for message type " +
                $"'{typeof(TMessage).FullName}'.",
                nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(
                definition.RoutingKey))
        {
            throw new ArgumentException(
                "RabbitMQ routing key is required for message type " +
                $"'{typeof(TMessage).FullName}'.",
                nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(
                definition.MessageType))
        {
            throw new ArgumentException(
                "RabbitMQ message contract name is required for " +
                $"CLR type '{typeof(TMessage).FullName}'.",
                nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(
                definition.MessageVersion))
        {
            throw new ArgumentException(
                "RabbitMQ message version is required for CLR type " +
                $"'{typeof(TMessage).FullName}'.",
                nameof(definition));
        }

        bool hasRetryExchange =
            !string.IsNullOrWhiteSpace(
                definition.RetryExchange);

        bool hasRetryRoutingKeyPrefix =
            !string.IsNullOrWhiteSpace(
                definition.RetryRoutingKeyPrefix);

        /*
         * Retry topology alanlarından yalnızca birinin verilmesi
         * geçersizdir.
         */
        if (hasRetryExchange !=
            hasRetryRoutingKeyPrefix)
        {
            throw new ArgumentException(
                "RabbitMQ retry exchange and retry routing key " +
                "prefix must either both be configured or both " +
                "be omitted for message type " +
                $"'{typeof(TMessage).FullName}'.",
                nameof(definition));
        }

        bool hasRetryTopology =
            hasRetryExchange &&
            hasRetryRoutingKeyPrefix;

        bool hasMaximumAttemptsOverride =
            definition.RetryMaximumAttempts.HasValue;

        int[]? retryDelaySeconds =
            definition.RetryDelaySeconds;

        bool hasDelaySecondsOverride =
            retryDelaySeconds is not null;

        /*
         * Retry policy override verilmişse route üzerinde retry
         * exchange ve routing key de tanımlı olmalıdır.
         */
        if ((hasMaximumAttemptsOverride ||
             hasDelaySecondsOverride) &&
            !hasRetryTopology)
        {
            throw new ArgumentException(
                "RabbitMQ retry policy overrides cannot be " +
                "configured without retry exchange and retry " +
                "routing key prefix.",
                nameof(definition));
        }

        int? retryMaximumAttempts =
            definition.RetryMaximumAttempts;

        if (retryMaximumAttempts.HasValue &&
            retryMaximumAttempts.Value < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    definition.RetryMaximumAttempts),
                retryMaximumAttempts.Value,
                "Route retry maximum attempts must be at least one.");
        }

        if (retryDelaySeconds is not null)
        {
            ValidateRetryDelaySeconds(
                retryDelaySeconds);
        }

        /*
         * İki override da verilmişse doğrudan doğrulanabilir.
         *
         * Yalnızca biri verilmişse diğeri global config
         * üzerinden geleceği için effective policy aşamasında
         * doğrulanacaktır.
         */
        if (retryMaximumAttempts.HasValue &&
            retryDelaySeconds is not null &&
            retryDelaySeconds.Length !=
            retryMaximumAttempts.Value - 1)
        {
            throw new ArgumentException(
                "Route retry delay count must equal " +
                "RetryMaximumAttempts minus one.",
                nameof(definition));
        }
    }

    private static void ValidateRetryDelaySeconds(
        int[] delaySeconds)
    {
        if (delaySeconds.Any(
                static delay => delay <= 0))
        {
            throw new ArgumentException(
                "Every route retry delay must be greater than zero.",
                nameof(delaySeconds));
        }

        if (delaySeconds
                .Distinct()
                .Count() !=
            delaySeconds.Length)
        {
            throw new ArgumentException(
                "Route retry delays must be unique.",
                nameof(delaySeconds));
        }
    }

    private static string? NullWhenWhiteSpace(
    string? value)
    {
        if (value is null)
        {
            return null;
        }

        string trimmedValue =
            value.Trim();

        if (trimmedValue.Length == 0)
        {
            return null;
        }

        return trimmedValue;
    }
}