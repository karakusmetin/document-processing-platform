using DocumentProcessing.Messaging.RabbitMq.Topology;

namespace DocumentProcessing.IntegrationTests
    .Messaging.Reliability;

internal sealed class ReliabilityTestTopologyDefinition :
    IRabbitMqTopologyDefinition
{
    private readonly RabbitMqReliabilityTestNames _names;

    public ReliabilityTestTopologyDefinition(
        RabbitMqReliabilityTestNames names)
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

        await DeclareExchangesAsync(
            builder,
            cancellationToken);

        await DeclareDeadLetterQueueAsync(
            builder,
            cancellationToken);

        await DeclareRequestQueueAsync(
            builder,
            cancellationToken);

        await DeclareRetryQueueAsync(
            builder,
            cancellationToken);
    }

    private async Task DeclareExchangesAsync(
        IRabbitMqTopologyBuilder builder,
        CancellationToken cancellationToken)
    {
        await builder.DeclareExchangeAsync(
            name:
                _names.CommandExchange,

            type:
                RabbitMqExchangeTypes.Direct,

            cancellationToken:
                cancellationToken);

        await builder.DeclareExchangeAsync(
            name:
                _names.RetryExchange,

            type:
                RabbitMqExchangeTypes.Direct,

            cancellationToken:
                cancellationToken);

        await builder.DeclareExchangeAsync(
            name:
                _names.DeadLetterExchange,

            type:
                RabbitMqExchangeTypes.Direct,

            cancellationToken:
                cancellationToken);
    }

    private async Task DeclareDeadLetterQueueAsync(
        IRabbitMqTopologyBuilder builder,
        CancellationToken cancellationToken)
    {
        await builder.DeclareQueueAsync(
            name:
                _names.DeadLetterQueue,

            cancellationToken:
                cancellationToken);

        await builder.BindQueueAsync(
            queue:
                _names.DeadLetterQueue,

            exchange:
                _names.DeadLetterExchange,

            routingKey:
                _names.DeadLetterRoutingKey,

            cancellationToken:
                cancellationToken);
    }

    private async Task DeclareRequestQueueAsync(
        IRabbitMqTopologyBuilder builder,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> arguments =
            new()
            {
                [
                    RabbitMqTopologyArgumentNames
                        .DeadLetterExchange
                ] =
                    _names.DeadLetterExchange,

                [
                    RabbitMqTopologyArgumentNames
                        .DeadLetterRoutingKey
                ] =
                    _names.DeadLetterRoutingKey
            };

        await builder.DeclareQueueAsync(
            name:
                _names.RequestQueue,

            arguments:
                arguments,

            cancellationToken:
                cancellationToken);

        await builder.BindQueueAsync(
            queue:
                _names.RequestQueue,

            exchange:
                _names.CommandExchange,

            routingKey:
                _names.RequestedRoutingKey,

            cancellationToken:
                cancellationToken);
    }

    private async Task DeclareRetryQueueAsync(
        IRabbitMqTopologyBuilder builder,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> arguments =
            new()
            {
                [
                    RabbitMqTopologyArgumentNames
                        .MessageTtl
                ] =
                    checked(
                        RabbitMqReliabilityTestNames
                            .RetryDelaySeconds *
                        1000),

                [
                    RabbitMqTopologyArgumentNames
                        .DeadLetterExchange
                ] =
                    _names.CommandExchange,

                [
                    RabbitMqTopologyArgumentNames
                        .DeadLetterRoutingKey
                ] =
                    _names.RequestedRoutingKey
            };

        await builder.DeclareQueueAsync(
            name:
                _names.RetryQueue,

            arguments:
                arguments,

            cancellationToken:
                cancellationToken);

        await builder.BindQueueAsync(
            queue:
                _names.RetryQueue,

            exchange:
                _names.RetryExchange,

            routingKey:
                _names.RetryRoutingKey,

            cancellationToken:
                cancellationToken);
    }
}