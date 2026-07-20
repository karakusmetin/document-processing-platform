namespace DocumentProcessing.Messaging.RabbitMq.Configuration;

public sealed class RabbitMqConsumerOptions
{
    public const string SectionName = "RabbitMq:Consumer";

    public ushort PrefetchCount { get; set; } = 1;

    public int ConcurrentConsumerCount { get; set; } = 1;

    public bool AutoAcknowledgement { get; set; } = false;

    public string ConsumerTagPrefix { get; set; } = "document-processing";

    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
}