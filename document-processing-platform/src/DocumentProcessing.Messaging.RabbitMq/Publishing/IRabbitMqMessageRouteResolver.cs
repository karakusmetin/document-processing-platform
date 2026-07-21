namespace DocumentProcessing.Messaging.RabbitMq.Publishing;

internal interface IRabbitMqMessageRouteResolver
{
    RabbitMqMessageRoute Resolve<TMessage>();
}