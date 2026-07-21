namespace DocumentProcessing.Messaging.RabbitMq.Publishing;

internal sealed record RabbitMqPublishDestination(string Exchange, string RoutingKey);