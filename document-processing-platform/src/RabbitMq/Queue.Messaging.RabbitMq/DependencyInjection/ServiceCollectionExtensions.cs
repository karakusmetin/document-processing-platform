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

namespace Queue.Messaging.RabbitMq.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

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
        ArgumentNullException.ThrowIfNull(services);

        services.AddHostedService<RabbitMqTopologyHostedService>();

        return services;
    }
}