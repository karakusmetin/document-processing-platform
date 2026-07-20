namespace DocumentProcessing.Messaging.RabbitMq.Configuration;

public sealed class RabbitMqPublisherOptions
{
    public const string SectionName = "RabbitMq:Publisher";

    public bool PublisherConfirmationsEnabled { get; set; } = true;

    public bool PublisherConfirmationTrackingEnabled { get; set; } = true;

    public bool Mandatory { get; set; } = true;

    public TimeSpan ConfirmationTimeout { get; set; } = TimeSpan.FromSeconds(15);

    public byte DeliveryMode { get; set; } = 2;
}