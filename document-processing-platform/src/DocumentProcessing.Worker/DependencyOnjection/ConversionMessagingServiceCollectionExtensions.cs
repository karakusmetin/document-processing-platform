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

        return services;
    }
}