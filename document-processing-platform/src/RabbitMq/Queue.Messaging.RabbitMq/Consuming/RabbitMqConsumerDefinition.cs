namespace Queue.Messaging.RabbitMq.Consuming;

/// <summary>
/// TMessage türündeki bir consumer'ın queue, mesaj
/// sözleşmesi ve endpoint bazlı runtime ayarlarını tanımlar.
/// </summary>
public sealed class RabbitMqConsumerDefinition<TMessage>
{
    public string QueueName { get; set; } =
        string.Empty;

    public string MessageType { get; set; } =
        string.Empty;

    public string MessageVersion { get; set; } =
        string.Empty;

    public string ConsumerTagPrefix { get; set; } =
        string.Empty;

    /*
     * Null değerler endpoint override'ı olmadığını gösterir.
     *
     * Bu durumda RabbitMqConsumerOptions içerisindeki global
     * değerler kullanılır.
     */

    public ushort? PrefetchCount { get; set; }

    public int? ConcurrentConsumerCount { get; set; }

    public TimeSpan? ShutdownTimeout { get; set; }
}