using DocumentProcessing.Messaging.RabbitMq.Configuration;
using DocumentProcessing.Messaging.RabbitMq.Topology;
using DocumentProcessing.Worker.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Worker.Topology;

internal sealed class ConversionRabbitMqTopologyDefinition :
    IRabbitMqTopologyDefinition
{
    private readonly ConversionRabbitMqTopologyOptions _topology;
    private readonly RabbitMqRetryOptions _retry;
    private readonly ILogger<
        ConversionRabbitMqTopologyDefinition> _logger;

    public ConversionRabbitMqTopologyDefinition(
        IOptions<ConversionRabbitMqTopologyOptions>
            topologyOptions,
        IOptions<RabbitMqRetryOptions> retryOptions,
        ILogger<ConversionRabbitMqTopologyDefinition> logger)
    {
        ArgumentNullException.ThrowIfNull(topologyOptions);
        ArgumentNullException.ThrowIfNull(retryOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _topology =
            topologyOptions.Value;

        _retry =
            retryOptions.Value;

        _logger =
            logger;
    }

    public string Name =>
        "document-processing.conversion";

    public int Order =>
        100;

    public async Task DeclareAsync(
        IRabbitMqTopologyBuilder builder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);

        await DeclareExchangesAsync(
                builder,
                cancellationToken)
            .ConfigureAwait(false);

        await DeclareDeadLetterQueueAsync(
                builder,
                cancellationToken)
            .ConfigureAwait(false);

        await DeclareRequestQueueAsync(
                builder,
                cancellationToken)
            .ConfigureAwait(false);

        await DeclareResultQueueAsync(
                builder,
                cancellationToken)
            .ConfigureAwait(false);

        await DeclareRetryQueuesAsync(
                builder,
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Document processing conversion topology declared.");
    }

    private async Task DeclareExchangesAsync(
        IRabbitMqTopologyBuilder builder,
        CancellationToken cancellationToken)
    {
        await builder
            .DeclareExchangeAsync(
                name:
                    _topology.CommandExchange,

                type:
                    RabbitMqExchangeTypes.Direct,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        await builder
            .DeclareExchangeAsync(
                name:
                    _topology.EventExchange,

                type:
                    RabbitMqExchangeTypes.Topic,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        await builder
            .DeclareExchangeAsync(
                name:
                    _topology.RetryExchange,

                type:
                    RabbitMqExchangeTypes.Direct,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        await builder
            .DeclareExchangeAsync(
                name:
                    _topology.DeadLetterExchange,

                type:
                    RabbitMqExchangeTypes.Direct,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task DeclareDeadLetterQueueAsync(
        IRabbitMqTopologyBuilder builder,
        CancellationToken cancellationToken)
    {
        await builder
            .DeclareQueueAsync(
                name:
                    _topology.ConversionDeadLetterQueue,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        await builder
            .BindQueueAsync(
                queue:
                    _topology.ConversionDeadLetterQueue,

                exchange:
                    _topology.DeadLetterExchange,

                routingKey:
                    _topology.ConversionDeadLetterRoutingKey,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);
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
                    _topology.DeadLetterExchange,

                [
                    RabbitMqTopologyArgumentNames
                        .DeadLetterRoutingKey
                ] =
                    _topology
                        .ConversionDeadLetterRoutingKey
            };

        await builder
            .DeclareQueueAsync(
                name:
                    _topology.ConversionRequestQueue,

                arguments:
                    arguments,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        await builder
            .BindQueueAsync(
                queue:
                    _topology.ConversionRequestQueue,

                exchange:
                    _topology.CommandExchange,

                routingKey:
                    _topology
                        .ConversionRequestedRoutingKey,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task DeclareResultQueueAsync(
        IRabbitMqTopologyBuilder builder,
        CancellationToken cancellationToken)
    {
        await builder
            .DeclareQueueAsync(
                name:
                    _topology.ConversionResultQueue,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        await builder
            .BindQueueAsync(
                queue:
                    _topology.ConversionResultQueue,

                exchange:
                    _topology.EventExchange,

                routingKey:
                    _topology
                        .ConversionCompletedRoutingKey,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        await builder
            .BindQueueAsync(
                queue:
                    _topology.ConversionResultQueue,

                exchange:
                    _topology.EventExchange,

                routingKey:
                    _topology
                        .ConversionFailedRoutingKey,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task DeclareRetryQueuesAsync(
        IRabbitMqTopologyBuilder builder,
        CancellationToken cancellationToken)
    {
        foreach (
            int delaySeconds
            in _retry.DelaySeconds)
        {
            string retryQueueName =
                RabbitMqTopologyNameBuilder
                    .GetRetryQueueName(
                        _topology.RetryQueuePrefix,
                        delaySeconds);

            string retryRoutingKey =
                RabbitMqTopologyNameBuilder
                    .GetRetryRoutingKey(
                        _topology.RetryRoutingKeyPrefix,
                        delaySeconds);

            int messageTtlMilliseconds =
                checked(
                    delaySeconds * 1000);

            Dictionary<string, object?> arguments =
                new()
                {
                    [
                        RabbitMqTopologyArgumentNames
                            .MessageTtl
                    ] =
                        messageTtlMilliseconds,

                    [
                        RabbitMqTopologyArgumentNames
                            .DeadLetterExchange
                    ] =
                        _topology.CommandExchange,

                    [
                        RabbitMqTopologyArgumentNames
                            .DeadLetterRoutingKey
                    ] =
                        _topology
                            .ConversionRequestedRoutingKey
                };

            await builder
                .DeclareQueueAsync(
                    name:
                        retryQueueName,

                    arguments:
                        arguments,

                    cancellationToken:
                        cancellationToken)
                .ConfigureAwait(false);

            await builder
                .BindQueueAsync(
                    queue:
                        retryQueueName,

                    exchange:
                        _topology.RetryExchange,

                    routingKey:
                        retryRoutingKey,

                    cancellationToken:
                        cancellationToken)
                .ConfigureAwait(false);
        }
    }
}