namespace Queue.Messaging.RabbitMq.Publishing;

/// <summary>
/// Belirli bir CLR mesaj türünün RabbitMQ yayın rotasını
/// ve route bazlı retry ayarlarını tanımlar.
/// </summary>
public sealed class RabbitMqMessageRouteDefinition<TMessage>
{
    /// <summary>
    /// Mesajın yayınlanacağı ana exchange.
    /// </summary>
    public string Exchange { get; set; } =
        string.Empty;

    /// <summary>
    /// Normal yayın routing key'i.
    /// </summary>
    public string RoutingKey { get; set; } =
        string.Empty;

    /// <summary>
    /// Envelope içerisinde taşınacak kararlı mesaj
    /// sözleşmesi adı.
    /// </summary>
    public string MessageType { get; set; } =
        string.Empty;

    /// <summary>
    /// Envelope içerisinde taşınacak mesaj
    /// sözleşmesi sürümü.
    /// </summary>
    public string MessageVersion { get; set; } =
        string.Empty;

    /// <summary>
    /// Mesaj delayed retry destekliyorsa retry exchange.
    /// </summary>
    public string? RetryExchange { get; set; }

    /// <summary>
    /// Retry routing key'lerinin üretileceği prefix.
    /// </summary>
    public string? RetryRoutingKeyPrefix { get; set; }

    /// <summary>
    /// Bu route için maksimum toplam işleme sayısıdır.
    ///
    /// İlk işleme de attempt sayısına dahildir.
    ///
    /// Null ise global RabbitMqRetryOptions değeri kullanılır.
    /// </summary>
    public int? RetryMaximumAttempts { get; set; }

    /// <summary>
    /// Bu route için retry gecikmeleridir.
    ///
    /// Örnek:
    /// [10, 60, 300]
    ///
    /// Null ise global RabbitMqRetryOptions değeri kullanılır.
    /// </summary>
    public int[]? RetryDelaySeconds { get; set; }
}