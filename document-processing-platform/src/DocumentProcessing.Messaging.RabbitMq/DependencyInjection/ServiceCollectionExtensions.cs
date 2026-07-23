using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Messaging.RabbitMq.Channels;
using DocumentProcessing.Messaging.RabbitMq.Configuration;
using DocumentProcessing.Messaging.RabbitMq.Connection;
using DocumentProcessing.Messaging.RabbitMq.Publishing;
using DocumentProcessing.Messaging.RabbitMq.Serialization;
using DocumentProcessing.Messaging.RabbitMq.Topology;
using DocumentProcessing.Messaging.RabbitMq.Retrying;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentProcessing.Messaging.RabbitMq.DependencyInjection;

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