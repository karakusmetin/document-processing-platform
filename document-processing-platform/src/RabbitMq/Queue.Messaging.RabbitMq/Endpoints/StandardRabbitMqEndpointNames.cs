namespace Queue.Messaging.RabbitMq.Endpoints.Standard;

/// <summary>
/// Standart RabbitMQ endpoint tarafından kullanılan fiziksel
/// exchange, queue, routing key ve consumer tag isimlerini
/// içerir.
/// </summary>
public sealed class StandardRabbitMqEndpointNames
{
    public string ExchangeName { get; set; } =
        string.Empty;

    public string QueueName { get; set; } =
        string.Empty;

    public string RoutingKey { get; set; } =
        string.Empty;

    public string RetryExchangeName { get; set; } =
        string.Empty;

    public string RetryQueueNamePrefix { get; set; } =
        string.Empty;

    public string RetryRoutingKeyPrefix { get; set; } =
        string.Empty;

    public string DeadLetterExchangeName { get; set; } =
        string.Empty;

    public string DeadLetterQueueName { get; set; } =
        string.Empty;

    public string DeadLetterRoutingKey { get; set; } =
        string.Empty;

    public string ConsumerTagPrefix { get; set; } =
        string.Empty;
}