namespace DocumentProcessing.Messaging.RabbitMq.Configuration;

public sealed class RabbitMqPublisherOptions
{
    public const string SectionName = "RabbitMq:Publisher";

    public string ProducerName { get; set; } = string.Empty;

    public TimeSpan ConfirmationTimeout { get; set; } = TimeSpan.FromSeconds(15);
}