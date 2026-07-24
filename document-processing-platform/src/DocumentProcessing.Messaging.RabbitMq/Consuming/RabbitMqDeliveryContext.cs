namespace DocumentProcessing.Messaging.RabbitMq.Consuming;

/// <summary>
/// Mesaj gövdesinden bağımsız RabbitMQ teslimat bilgilerini handler'a taşır.
/// </summary>
public sealed record RabbitMqDeliveryContext
{
    public required bool Redelivered { get; init; }

    public required string Exchange { get; init; }

    public required string RoutingKey { get; init; }

    public string? BrokerMessageId { get; init; }

    public string? BrokerCorrelationId { get; init; }

    public string? BrokerMessageType { get; init; }
}