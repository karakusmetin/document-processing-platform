namespace DocumentProcessing.Messaging.RabbitMq.Publishing;

/// <summary>
/// Belirli bir CLR mesaj türünün RabbitMQ yayın rotasını
/// tanımlar.
/// </summary>
public sealed class RabbitMqMessageRouteDefinition<TMessage>
{
    /// <summary>
    /// Mesajın yayınlanacağı exchange.
    /// </summary>
    public string Exchange { get; set; } =
        string.Empty;

    /// <summary>
    /// Normal yayın routing key'i.
    /// </summary>
    public string RoutingKey { get; set; } =
        string.Empty;

    /// <summary>
    /// Envelope içerisinde taşınacak mesaj sözleşmesi adı.
    /// </summary>
    public string MessageType { get; set; } =
        string.Empty;

    /// <summary>
    /// Envelope içerisinde taşınacak mesaj sözleşmesi sürümü.
    /// </summary>
    public string MessageVersion { get; set; } =
        string.Empty;

    /// <summary>
    /// Mesaj delayed retry destekliyorsa retry exchange.
    /// </summary>
    public string? RetryExchange { get; set; }

    /// <summary>
    /// Retry queue routing key'lerinin üretileceği prefix.
    /// </summary>
    public string? RetryRoutingKeyPrefix { get; set; }
}