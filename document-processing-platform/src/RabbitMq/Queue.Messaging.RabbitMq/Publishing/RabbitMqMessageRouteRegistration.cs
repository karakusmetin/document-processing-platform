namespace Queue.Messaging.RabbitMq.Publishing;

internal sealed class RabbitMqMessageRouteRegistration<TMessage> :
    IRabbitMqMessageRouteRegistration
{
    public RabbitMqMessageRouteRegistration(
        RabbitMqMessageRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);

        Route = route;
    }

    public Type MessageClrType =>
        typeof(TMessage);

    public RabbitMqMessageRoute Route { get; }
}