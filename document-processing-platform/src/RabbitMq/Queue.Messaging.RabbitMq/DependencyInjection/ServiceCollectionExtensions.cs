using Queue.Messaging.RabbitMq.Channels;
using Queue.Messaging.RabbitMq.Configuration;
using Queue.Messaging.RabbitMq.Connection;
using Queue.Messaging.RabbitMq.Publishing;
using Queue.Messaging.RabbitMq.Serialization;
using Queue.Messaging.RabbitMq.Topology;
using Queue.Messaging.RabbitMq.Retrying;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Queue.Messaging.Abstractions;
using Queue.Messaging.RabbitMq.Compatibility;

namespace Queue.Messaging.RabbitMq.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        Guard.NotNull(services, nameof(services));
        Guard.NotNull(configuration, nameof(configuration));

        services.AddRabbitMqOptions(configuration);

        services.AddSingleton<IMessageSerializer,SystemTextJsonMessageSerializer>();

        services.AddSingleton<IRabbitMqConnectionProvider,RabbitMqConnectionProvider>();

        services.AddSingleton<IRabbitMqChannelFactory,RabbitMqChannelFactory>();

        services.AddSingleton<IRabbitMqTopologyInitializer,RabbitMqTopologyInitializer>();
        
        services.AddSingleton<IRabbitMqMessageRouteResolver,RabbitMqMessageRouteResolver>();

        services.AddSingleton<IRabbitMqPublisher,RabbitMqPublisher>();

        services.AddSingleton<IMessagePublisher,RabbitMqMessagePublisher>();

        services.AddSingleton<IMessageRetryScheduler,RabbitMqMessageRetryScheduler>();

        return services;
    }
    public static IServiceCollection AddRabbitMqTopologyInitialization(this IServiceCollection services)
    {
        Guard.NotNull(services, nameof(services));

        services.AddHostedService<RabbitMqTopologyHostedService>();

        return services;
    }
}