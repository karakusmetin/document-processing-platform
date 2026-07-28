using Queue.Messaging.Abstractions;
using Queue.Messaging.RabbitMq.Configuration;
using Microsoft.Extensions.Options;
using Queue.Messaging.RabbitMq.Compatibility;


namespace Queue.Messaging.RabbitMq.Publishing;

internal sealed class RabbitMqMessagePublisher :
    IMessagePublisher
{
    private readonly IRabbitMqMessageRouteResolver _routeResolver;
    private readonly IRabbitMqPublisher _rabbitMqPublisher;
    private readonly RabbitMqPublisherOptions _publisherOptions;

    public RabbitMqMessagePublisher(
        IRabbitMqMessageRouteResolver routeResolver,
        IRabbitMqPublisher rabbitMqPublisher,
        IOptions<RabbitMqPublisherOptions> publisherOptions)
    {
        Guard.NotNull(routeResolver, nameof(routeResolver));
        Guard.NotNull(rabbitMqPublisher, nameof(rabbitMqPublisher));
        Guard.NotNull(publisherOptions, nameof(publisherOptions));

        _routeResolver = routeResolver;
        _rabbitMqPublisher = rabbitMqPublisher;
        _publisherOptions = publisherOptions.Value;
    }

    public async Task PublishAsync<TMessage>(
        TMessage message,
        MessagePublishContext context,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(message, nameof(message));
        Guard.NotNull(context, nameof(context));

        if (context.Attempt < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(context),
                context.Attempt,
                "Message attempt must be greater than zero.");
        }

        RabbitMqMessageRoute route =
            _routeResolver.Resolve<TMessage>();

        MessageEnvelope<TMessage> envelope =
            MessageEnvelope<TMessage>.Create(
                payload: message,
                messageType: route.MessageType,
                messageVersion: route.MessageVersion,
                producer: _publisherOptions.ProducerName,
                correlationId: context.CorrelationId,
                causationId: context.CausationId,
                attempt: context.Attempt,
                messageId: context.MessageId);

        RabbitMqPublishDestination destination =
            new(
                route.Exchange,
                route.RoutingKey);

        await _rabbitMqPublisher
            .PublishAsync(
                envelope,
                destination,
                cancellationToken)
            .ConfigureAwait(false);
    }
}