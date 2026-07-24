using DocumentProcessing.Messaging.RabbitMq.Topology;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentProcessing.Messaging.RabbitMq.DependencyInjection;

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
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<
            IRabbitMqTopologyDefinition,
            TDefinition>();

        return services;
    }
}