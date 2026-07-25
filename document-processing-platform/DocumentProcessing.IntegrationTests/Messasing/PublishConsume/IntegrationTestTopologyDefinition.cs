using DocumentProcessing.Messaging.RabbitMq.Topology;

namespace DocumentProcessing.IntegrationTests
    .Messaging.PublishConsume;

internal sealed class IntegrationTestTopologyDefinition :
    IRabbitMqTopologyDefinition
{
    private readonly RabbitMqIntegrationTestNames _names;

    public IntegrationTestTopologyDefinition(
        RabbitMqIntegrationTestNames names)
    {
        ArgumentNullException.ThrowIfNull(names);

        _names = names;
    }

    public string Name =>
        _names.DefinitionName;

    public int Order =>
        100;

    public async Task DeclareAsync(
        IRabbitMqTopologyBuilder builder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);

        await builder
            .DeclareExchangeAsync(
                name:
                    _names.ExchangeName,

                type:
                    RabbitMqExchangeTypes.Direct,

                cancellationToken:
                    cancellationToken);

        await builder
            .DeclareQueueAsync(
                name:
                    _names.QueueName,

                cancellationToken:
                    cancellationToken);

        await builder
            .BindQueueAsync(
                queue:
                    _names.QueueName,

                exchange:
                    _names.ExchangeName,

                routingKey:
                    _names.RoutingKey,

                cancellationToken:
                    cancellationToken);
    }
}