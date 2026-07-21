using DocumentProcessing.Contracts.Messages;
using DocumentProcessing.Messaging.RabbitMq.Configuration;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Messaging.RabbitMq.Publishing;

internal sealed class RabbitMqMessageRouteResolver : IRabbitMqMessageRouteResolver
{
    private readonly RabbitMqTopologyOptions _topologyOptions;

    public RabbitMqMessageRouteResolver(
        IOptions<RabbitMqTopologyOptions> topologyOptions)
    {
        ArgumentNullException.ThrowIfNull(topologyOptions);

        _topologyOptions = topologyOptions.Value;
    }

    public RabbitMqMessageRoute Resolve<TMessage>()
    {
        Type messageType = typeof(TMessage);

        if (messageType == typeof(ConversionRequested))
        {
            return new RabbitMqMessageRoute(
                Exchange:
                    _topologyOptions.CommandExchange,
                RoutingKey:
                    _topologyOptions
                        .ConversionRequestedRoutingKey,
                MessageType:
                    ConversionMessageTypes
                        .ConversionRequested,
                MessageVersion:
                    ConversionMessageVersions.V1);
        }

        if (messageType == typeof(ConversionCompleted))
        {
            return new RabbitMqMessageRoute(
                Exchange:
                    _topologyOptions.EventExchange,
                RoutingKey:
                    _topologyOptions
                        .ConversionCompletedRoutingKey,
                MessageType:
                    ConversionMessageTypes
                        .ConversionCompleted,
                MessageVersion:
                    ConversionMessageVersions.V1);
        }

        if (messageType == typeof(ConversionFailed))
        {
            return new RabbitMqMessageRoute(
                Exchange:
                    _topologyOptions.EventExchange,
                RoutingKey:
                    _topologyOptions
                        .ConversionFailedRoutingKey,
                MessageType:
                    ConversionMessageTypes
                        .ConversionFailed,
                MessageVersion:
                    ConversionMessageVersions.V1);
        }

        throw new NotSupportedException(
            $"No RabbitMQ route is registered for message type " +
            $"'{messageType.FullName}'.");
    }
}