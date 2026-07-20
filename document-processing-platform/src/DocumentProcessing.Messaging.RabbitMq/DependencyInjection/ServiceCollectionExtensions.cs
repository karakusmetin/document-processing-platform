using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Messaging.RabbitMq.Options;
using DocumentProcessing.Messaging.RabbitMq.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentProcessing.Messaging.RabbitMq.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.HostName), "RabbitMQ HostName is required")
            .ValidateOnStart();

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<RabbitMqTopologyInitializer>();
        services.AddSingleton<IIntegrationEventPublisher, RabbitMqEventPublisher>();
        return services;
    }
}
