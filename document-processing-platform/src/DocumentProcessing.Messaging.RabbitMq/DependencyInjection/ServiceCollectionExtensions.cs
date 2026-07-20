using DocumentProcessing.Messaging.RabbitMq.Channels;
using DocumentProcessing.Messaging.RabbitMq.Configuration;
using DocumentProcessing.Messaging.RabbitMq.Connection;
using DocumentProcessing.Messaging.RabbitMq.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentProcessing.Messaging.RabbitMq.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddRabbitMqOptions(configuration);

        services.AddSingleton<IMessageSerializer,SystemTextJsonMessageSerializer>();

        services.AddSingleton<IRabbitMqConnectionProvider,RabbitMqConnectionProvider>();

        services.AddSingleton<IRabbitMqChannelFactory,RabbitMqChannelFactory>();

        return services;
    }
}