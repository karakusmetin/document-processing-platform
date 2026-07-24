using DocumentProcessing.Contracts.Messages;
using DocumentProcessing.Messaging.RabbitMq.Configuration;
using DocumentProcessing.Messaging.RabbitMq.DependencyInjection;
using DocumentProcessing.Worker.Consumers;
using DocumentProcessing.Worker.Consumers.Retry;

namespace DocumentProcessing.Worker.DependencyInjection;

internal static class
    ConversionMessagingServiceCollectionExtensions
{
    public static IServiceCollection AddConversionMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection topologySection =
            configuration.GetSection(
                RabbitMqTopologyOptions.SectionName);

        string conversionRequestQueue =
            topologySection[
                nameof(
                    RabbitMqTopologyOptions
                        .ConversionRequestQueue)]
            ?? throw new InvalidOperationException(
                "RabbitMQ conversion request queue is not configured.");

        IConfigurationSection consumerSection =
            configuration.GetSection(
                RabbitMqConsumerOptions.SectionName);

        string commandExchange =
            GetRequiredValue(
                topologySection,
                nameof(
                    RabbitMqTopologyOptions.CommandExchange));

        string eventExchange =
            GetRequiredValue(
                topologySection,
                nameof(
                    RabbitMqTopologyOptions.EventExchange));

        string retryExchange =
            GetRequiredValue(
                topologySection,
                nameof(
                    RabbitMqTopologyOptions.RetryExchange));

        string conversionRequestedRoutingKey =
            GetRequiredValue(
                topologySection,
                nameof(
                    RabbitMqTopologyOptions
                        .ConversionRequestedRoutingKey));

        string conversionCompletedRoutingKey =
            GetRequiredValue(
                topologySection,
                nameof(
                    RabbitMqTopologyOptions
                        .ConversionCompletedRoutingKey));

        string conversionFailedRoutingKey =
            GetRequiredValue(
                topologySection,
                nameof(
                    RabbitMqTopologyOptions
                        .ConversionFailedRoutingKey));

        string retryRoutingKeyPrefix =
            GetRequiredValue(
                topologySection,
                nameof(
                    RabbitMqTopologyOptions
                        .RetryRoutingKeyPrefix));

        string configuredConsumerTagPrefix =
            consumerSection[
                nameof(
                    RabbitMqConsumerOptions
                        .ConsumerTagPrefix)]
            ?? "document-processing";

        services.AddSingleton<
            IRetryDelayProvider,
            ConfiguredRetryDelayProvider>();

        services.AddRabbitMqConsumer<
            ConversionRequested,
            ConversionRequestMessageHandler>(
            definition =>
            {
                definition.QueueName =
                    conversionRequestQueue;

                definition.MessageType =
                    ConversionMessageTypes
                        .ConversionRequested;

                definition.MessageVersion =
                    ConversionMessageVersions.V1;

                definition.ConsumerTagPrefix =
                    $"{configuredConsumerTagPrefix}.conversion";
            });
        services.AddRabbitMqMessageRoute<
            ConversionRequested>(
            route =>
            {
                route.Exchange =
                    commandExchange;

                route.RoutingKey =
                    conversionRequestedRoutingKey;

                route.MessageType =
                    ConversionMessageTypes
                        .ConversionRequested;

                route.MessageVersion =
                    ConversionMessageVersions.V1;

                /*
                 * ConversionRequested delayed retry destekliyor.
                 */
                route.RetryExchange =
                    retryExchange;

                route.RetryRoutingKeyPrefix =
                    retryRoutingKeyPrefix;
            });

        services.AddRabbitMqMessageRoute<
            ConversionCompleted>(
            route =>
            {
                route.Exchange =
                    eventExchange;

                route.RoutingKey =
                    conversionCompletedRoutingKey;

                route.MessageType =
                    ConversionMessageTypes
                        .ConversionCompleted;

                route.MessageVersion =
                    ConversionMessageVersions.V1;

                /*
                 * Result eventleri conversion retry queue'suna
                 * gönderilmez. Retry alanları boş bırakılır.
                 */
            });

        services.AddRabbitMqMessageRoute<
            ConversionFailed>(
            route =>
            {
                route.Exchange =
                    eventExchange;

                route.RoutingKey =
                    conversionFailedRoutingKey;

                route.MessageType =
                    ConversionMessageTypes
                        .ConversionFailed;

                route.MessageVersion =
                    ConversionMessageVersions.V1;
            });
        return services;
    }
    private static string GetRequiredValue(
    IConfigurationSection section,
    string propertyName)
    {
        string? value =
            section[propertyName];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Required RabbitMQ configuration value " +
                $"'{section.Path}:{propertyName}' is missing.");
        }

        return value;
    }
}