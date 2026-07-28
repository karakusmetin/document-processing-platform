using Queue.Messaging.RabbitMq.Compatibility;

namespace Queue.Messaging.RabbitMq.Publishing;

internal sealed class RabbitMqMessageRouteRegistration<TMessage> :
    IRabbitMqMessageRouteRegistration
{
    public RabbitMqMessageRouteRegistration(
        RabbitMqMessageRoute route)
    {
        Guard.NotNull(route, nameof(route));

        Route = route;
    }

    public Type MessageClrType =>
        typeof(TMessage);

    public RabbitMqMessageRoute Route { get; }
}