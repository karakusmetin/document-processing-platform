using Queue.Messaging.RabbitMq.Publishing;
using Microsoft.Extensions.DependencyInjection;

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
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        RabbitMqMessageRouteDefinition<TMessage> definition =
            new();

        configure(definition);

        ValidateDefinition(definition);

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
                        definition.RetryRoutingKeyPrefix));

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
                $"RabbitMQ exchange is required for message type " +
                $"'{typeof(TMessage).FullName}'.",
                nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(
                definition.RoutingKey))
        {
            throw new ArgumentException(
                $"RabbitMQ routing key is required for message type " +
                $"'{typeof(TMessage).FullName}'.",
                nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(
                definition.MessageType))
        {
            throw new ArgumentException(
                $"RabbitMQ message contract name is required for " +
                $"CLR type '{typeof(TMessage).FullName}'.",
                nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(
                definition.MessageVersion))
        {
            throw new ArgumentException(
                $"RabbitMQ message version is required for CLR type " +
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
         * Retry alanlarından yalnızca birinin verilmesi
         * geçersizdir.
         *
         * Retry destekleniyorsa ikisi de verilmelidir.
         * Desteklenmiyorsa ikisi de boş olmalıdır.
         */
        if (hasRetryExchange !=
            hasRetryRoutingKeyPrefix)
        {
            throw new ArgumentException(
                $"RabbitMQ retry exchange and retry routing key " +
                $"prefix must either both be configured or both " +
                $"be omitted for message type " +
                $"'{typeof(TMessage).FullName}'.",
                nameof(definition));
        }
    }

    private static string? NullWhenWhiteSpace(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }
}