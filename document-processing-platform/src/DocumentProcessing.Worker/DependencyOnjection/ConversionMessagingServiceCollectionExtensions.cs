using DocumentProcessing.Contracts.Messages;
using Queue.Messaging.RabbitMq.Configuration;
using Queue.Messaging.RabbitMq.DependencyInjection;
using DocumentProcessing.Worker.Configuration;
using DocumentProcessing.Worker.Consumers;
using DocumentProcessing.Worker.Consumers.Retry;
using DocumentProcessing.Worker.Topology;
using Microsoft.Extensions.Options;

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
                ConversionRabbitMqTopologyOptions.SectionName);

        ConversionRabbitMqTopologyOptions topologyOptions =
            new()
            {
                CommandExchange =
                    GetRequiredValue(
                        topologySection,
                        nameof(
                            ConversionRabbitMqTopologyOptions
                                .CommandExchange)),

                EventExchange =
                    GetRequiredValue(
                        topologySection,
                        nameof(
                            ConversionRabbitMqTopologyOptions
                                .EventExchange)),

                RetryExchange =
                    GetRequiredValue(
                        topologySection,
                        nameof(
                            ConversionRabbitMqTopologyOptions
                                .RetryExchange)),

                DeadLetterExchange =
                    GetRequiredValue(
                        topologySection,
                        nameof(
                            ConversionRabbitMqTopologyOptions
                                .DeadLetterExchange)),

                ConversionRequestQueue =
                    GetRequiredValue(
                        topologySection,
                        nameof(
                            ConversionRabbitMqTopologyOptions
                                .ConversionRequestQueue)),

                ConversionResultQueue =
                    GetRequiredValue(
                        topologySection,
                        nameof(
                            ConversionRabbitMqTopologyOptions
                                .ConversionResultQueue)),

                ConversionDeadLetterQueue =
                    GetRequiredValue(
                        topologySection,
                        nameof(
                            ConversionRabbitMqTopologyOptions
                                .ConversionDeadLetterQueue)),

                ConversionRequestedRoutingKey =
                    GetRequiredValue(
                        topologySection,
                        nameof(
                            ConversionRabbitMqTopologyOptions
                                .ConversionRequestedRoutingKey)),

                ConversionCompletedRoutingKey =
                    GetRequiredValue(
                        topologySection,
                        nameof(
                            ConversionRabbitMqTopologyOptions
                                .ConversionCompletedRoutingKey)),

                ConversionFailedRoutingKey =
                    GetRequiredValue(
                        topologySection,
                        nameof(
                            ConversionRabbitMqTopologyOptions
                                .ConversionFailedRoutingKey)),

                ConversionDeadLetterRoutingKey =
                    GetRequiredValue(
                        topologySection,
                        nameof(
                            ConversionRabbitMqTopologyOptions
                                .ConversionDeadLetterRoutingKey)),

                RetryQueuePrefix =
                    GetRequiredValue(
                        topologySection,
                        nameof(
                            ConversionRabbitMqTopologyOptions
                                .RetryQueuePrefix)),

                RetryRoutingKeyPrefix =
                    GetRequiredValue(
                        topologySection,
                        nameof(
                            ConversionRabbitMqTopologyOptions
                                .RetryRoutingKeyPrefix))
            };

        /*
         * Topology definition ve route registration aynı
         * doğrulanmış options instance'ını kullanır.
         */
        services.AddSingleton<
            IOptions<ConversionRabbitMqTopologyOptions>>(
            Options.Create(topologyOptions));

        services
            .AddRabbitMqTopologyDefinition<
                ConversionRabbitMqTopologyDefinition>();

        services.AddSingleton<
            IRetryDelayProvider,
            ConfiguredRetryDelayProvider>();

        services.AddRabbitMqMessageRoute<
            ConversionRequested>(
            route =>
            {
                route.Exchange =
                    topologyOptions.CommandExchange;

                route.RoutingKey =
                    topologyOptions
                        .ConversionRequestedRoutingKey;

                route.MessageType =
                    ConversionMessageTypes
                        .ConversionRequested;

                route.MessageVersion =
                    ConversionMessageVersions.V1;

                route.RetryExchange =
                    topologyOptions.RetryExchange;

                route.RetryRoutingKeyPrefix =
                    topologyOptions
                        .RetryRoutingKeyPrefix;
            });

        services.AddRabbitMqMessageRoute<
            ConversionCompleted>(
            route =>
            {
                route.Exchange =
                    topologyOptions.EventExchange;

                route.RoutingKey =
                    topologyOptions
                        .ConversionCompletedRoutingKey;

                route.MessageType =
                    ConversionMessageTypes
                        .ConversionCompleted;

                route.MessageVersion =
                    ConversionMessageVersions.V1;
            });

        services.AddRabbitMqMessageRoute<
            ConversionFailed>(
            route =>
            {
                route.Exchange =
                    topologyOptions.EventExchange;

                route.RoutingKey =
                    topologyOptions
                        .ConversionFailedRoutingKey;

                route.MessageType =
                    ConversionMessageTypes
                        .ConversionFailed;

                route.MessageVersion =
                    ConversionMessageVersions.V1;
            });

        IConfigurationSection consumerSection =
            configuration.GetSection(
                RabbitMqConsumerOptions.SectionName);

        string configuredConsumerTagPrefix =
            consumerSection[
                nameof(
                    RabbitMqConsumerOptions
                        .ConsumerTagPrefix)]
            ?? "document-processing";

        services.AddRabbitMqConsumer<
            ConversionRequested,
            ConversionRequestMessageHandler>(
            definition =>
            {
                definition.QueueName =
                    topologyOptions
                        .ConversionRequestQueue;

                definition.MessageType =
                    ConversionMessageTypes
                        .ConversionRequested;

                definition.MessageVersion =
                    ConversionMessageVersions.V1;

                definition.ConsumerTagPrefix =
                    $"{configuredConsumerTagPrefix}.conversion";
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