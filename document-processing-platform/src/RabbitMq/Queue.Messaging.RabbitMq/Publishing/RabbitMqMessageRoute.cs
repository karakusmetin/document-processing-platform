namespace Queue.Messaging.RabbitMq.Publishing;

internal sealed record RabbitMqMessageRoute(
    string Exchange,
    string RoutingKey,
    string MessageType,
    string MessageVersion,
    string? RetryExchange,
    string? RetryRoutingKeyPrefix);