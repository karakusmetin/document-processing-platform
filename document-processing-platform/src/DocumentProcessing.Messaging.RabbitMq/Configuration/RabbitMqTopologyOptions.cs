namespace DocumentProcessing.Messaging.RabbitMq.Configuration;

public sealed class RabbitMqTopologyOptions
{
    public const string SectionName = "RabbitMq:Topology";

    public string CommandExchange { get; set; } = "document.processing.commands.v1";

    public string EventExchange { get; set; } = "document.processing.events.v1";

    public string DeadLetterExchange { get; set; } = "document.processing.dead-letter.v1";

    public string ConversionRequestQueue { get; set; } = "document.processing.conversion.request.v1";

    public string ConversionDeadLetterQueue { get; set; } = "document.processing.conversion.dead.v1";

    public string ConversionRequestedRoutingKey { get; set; } = "conversion.requested.v1";

    public string ConversionCompletedRoutingKey { get; set; } = "conversion.completed.v1";

    public string ConversionFailedRoutingKey { get; set; } = "conversion.failed.v1";

    public string ConversionDeadLetterRoutingKey { get; set; } = "conversion.dead.v1";

    public string RetryQueuePrefix { get; set; } = "document.processing.conversion.retry";
}