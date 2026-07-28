using Queue.Messaging.RabbitMq.Topology;
using Microsoft.Extensions.DependencyInjection;
using Queue.Messaging.RabbitMq.Compatibility;

namespace Queue.Messaging.RabbitMq.DependencyInjection;

public static class
    RabbitMqTopologyDefinitionServiceCollectionExtensions
{
    public static IServiceCollection
        AddRabbitMqTopologyDefinition<TDefinition>(
            this IServiceCollection services)
        where TDefinition :
            class,
            IRabbitMqTopologyDefinition
    {
        Guard.NotNull(services, nameof(services));

        services.AddSingleton<
            IRabbitMqTopologyDefinition,
            TDefinition>();

        return services;
    }
}