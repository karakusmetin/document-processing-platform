namespace DocumentProcessing.Messaging.RabbitMq.Publishing;

internal interface IRabbitMqMessageRouteRegistration
{
    Type MessageClrType { get; }

    RabbitMqMessageRoute Route { get; }
}