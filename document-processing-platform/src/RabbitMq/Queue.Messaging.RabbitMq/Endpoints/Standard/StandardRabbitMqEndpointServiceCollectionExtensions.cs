using Microsoft.Extensions.DependencyInjection;
using Queue.Messaging.RabbitMq.Compatibility;
using Queue.Messaging.RabbitMq.Consuming;
using Queue.Messaging.RabbitMq.DependencyInjection;
using Queue.Messaging.RabbitMq.Topology;

namespace Queue.Messaging.RabbitMq.Endpoints.Standard;

public static class
    StandardRabbitMqEndpointServiceCollectionExtensions
{
    /// <summary>
    /// Route, consumer, retry topology ve dead-letter
    /// topology kayıtlarını tek bir çağrıyla ekler.
    /// </summary>
    public static IServiceCollection
        AddStandardRabbitMqEndpoint<
            TMessage,
            THandler>(
            this IServiceCollection services,
            string endpointName,
            Action<StandardRabbitMqEndpointOptions>?
                configure = null)
        where THandler :
            class,
            IRabbitMqMessageHandler<TMessage>
    {
        Guard.NotNull(
            services,
            nameof(services));

        StandardRabbitMqEndpointOptions options =
            new(endpointName)
            {
                /*
                 * Varsayılan değer kararlı bir CLR type adıdır.
                 * Uygulama isterse configure callback içinde
                 * business sözleşme adıyla değiştirebilir.
                 */
                MessageType =
                    typeof(TMessage).FullName
                    ?? typeof(TMessage).Name
            };

        configure?.Invoke(
            options);

        StandardRabbitMqEndpointValidator
            .Validate<TMessage>(
                options);

        ValidateDuplicateRegistration<TMessage>(
            services,
            options.EndpointName);

        StandardRabbitMqEndpointRegistration<TMessage>
            registration =
                new(options);

        StandardRabbitMqEndpointNames names =
            registration.Names;

        /*
         * Publisher route kaydı.
         */
        services.AddRabbitMqMessageRoute<TMessage>(
            definition =>
            {
                definition.Exchange =
                    names.ExchangeName;

                definition.RoutingKey =
                    names.RoutingKey;

                definition.MessageType =
                    registration.MessageType;

                definition.MessageVersion =
                    registration.MessageVersion;

                definition.RetryExchange =
                    names.RetryExchangeName;

                definition.RetryRoutingKeyPrefix =
                    names.RetryRoutingKeyPrefix;

                definition.RetryMaximumAttempts =
                    registration.MaximumAttempts;

                definition.RetryDelaySeconds =
                    registration.DelaySeconds?
                        .ToArray();
            });

        /*
         * Consumer ve handler kaydı.
         */
        services.AddRabbitMqConsumer<
            TMessage,
            THandler>(
            definition =>
            {
                definition.QueueName =
                    names.QueueName;

                definition.MessageType =
                    registration.MessageType;

                definition.MessageVersion =
                    registration.MessageVersion;

                definition.ConsumerTagPrefix =
                    names.ConsumerTagPrefix;

                definition.PrefetchCount =
                    registration.PrefetchCount;

                definition.ConcurrentConsumerCount =
                    registration
                        .ConcurrentConsumerCount;

                definition.ShutdownTimeout =
                    registration.ShutdownTimeout;
            });

        /*
         * Endpoint-specific registration snapshot.
         */
        services.AddSingleton(
            registration);

        /*
         * Duplicate endpoint kontrolleri için marker.
         */
        services.AddSingleton(
            new StandardRabbitMqEndpointMarker(
                registration.EndpointName,
                typeof(TMessage)));

        /*
         * Standard topology definition.
         */
        services.AddSingleton<
            IRabbitMqTopologyDefinition,
            StandardRabbitMqEndpointTopologyDefinition<
                TMessage>>();

        /*
         * Standard endpoint kullanıldığında topology
         * initializer otomatik olarak eklenir.
         */
        services.AddRabbitMqTopologyInitialization();

        return services;
    }

    private static void ValidateDuplicateRegistration<
        TMessage>(
        IServiceCollection services,
        string endpointName)
    {
        StandardRabbitMqEndpointMarker[] existingMarkers =
            services
                .Where(
                    static descriptor =>
                        descriptor.ServiceType ==
                        typeof(
                            StandardRabbitMqEndpointMarker))
                .Select(
                    static descriptor =>
                        descriptor.ImplementationInstance
                        as StandardRabbitMqEndpointMarker)
                .Where(
                    static marker =>
                        marker is not null)
                .Cast<
                    StandardRabbitMqEndpointMarker>()
                .ToArray();

        StandardRabbitMqEndpointMarker?
            duplicateEndpoint =
                existingMarkers.FirstOrDefault(
                    marker =>
                        string.Equals(
                            marker.EndpointName,
                            endpointName,
                            StringComparison.Ordinal));

        if (duplicateEndpoint is not null)
        {
            throw new InvalidOperationException(
                "A standard RabbitMQ endpoint is already " +
                $"registered with name '{endpointName}'. " +
                "Existing message type: " +
                $"'{duplicateEndpoint.MessageClrType.FullName}'.");
        }

        Type messageClrType =
            typeof(TMessage);

        StandardRabbitMqEndpointMarker?
            duplicateMessageType =
                existingMarkers.FirstOrDefault(
                    marker =>
                        marker.MessageClrType ==
                        messageClrType);

        if (duplicateMessageType is not null)
        {
            throw new InvalidOperationException(
                "A standard RabbitMQ endpoint is already " +
                "registered for CLR message type " +
                $"'{messageClrType.FullName}'. Existing endpoint: " +
                $"'{duplicateMessageType.EndpointName}'.");
        }
    }
}