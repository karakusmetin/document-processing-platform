namespace DocumentProcessing.Messaging.RabbitMq.Consuming;

/// <summary>
/// TMessage türündeki bir consumer'ın queue ve mesaj
/// sözleşmesi tanımıdır.
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
}