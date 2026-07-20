namespace DocumentProcessing.Messaging.RabbitMq.Configuration;

public sealed class RabbitMqRetryOptions
{
    public const string SectionName = "RabbitMq:Retry";

    public int MaximumAttempts { get; set; } = 4;

    public int[] DelaySeconds { get; set; } =
    [
        10,
        60,
        300
    ];

    public bool PublishFailedEventWhenExhausted { get; set; } = true;
}