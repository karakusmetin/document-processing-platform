using Queue.Messaging.RabbitMq.Consuming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Queue.Messaging.RabbitMq.Compatibility;

namespace Queue.Messaging.RabbitMq.DependencyInjection;

public static class RabbitMqConsumerServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqConsumer<
        TMessage,
        THandler>(
        this IServiceCollection services,
        Action<RabbitMqConsumerDefinition<TMessage>>
            configure)
        where THandler :
            class,
            IRabbitMqMessageHandler<TMessage>
    {
        Guard.NotNull(services, nameof(services));
        Guard.NotNull(configure, nameof(configure));

        services
            .AddOptions<
                RabbitMqConsumerDefinition<TMessage>>()
            .Configure(configure)
            .Validate(
                static definition =>
                    !string.IsNullOrWhiteSpace(
                        definition.QueueName),
                "RabbitMQ consumer queue name is required.")
            .Validate(
                static definition =>
                    !string.IsNullOrWhiteSpace(
                        definition.MessageType),
                "RabbitMQ consumer message type is required.")
            .Validate(
                static definition =>
                    !string.IsNullOrWhiteSpace(
                        definition.MessageVersion),
                "RabbitMQ consumer message version is required.")
            .Validate(
                static definition =>
                    !string.IsNullOrWhiteSpace(
                        definition.ConsumerTagPrefix),
                "RabbitMQ consumer tag prefix is required.")
            .ValidateOnStart();

        /*
         * Handler mesaj başına oluşturulacak scope içinden
         * resolve edilir.
         */
        services.AddScoped<IRabbitMqMessageHandler<TMessage>,THandler>();

        /*
         * Her TMessage için ayrı hosted service oluşturulur.
         */
        services.AddSingleton<IHostedService,RabbitMqConsumerHostedService<TMessage>>();

        return services;
    }
}