namespace DocumentProcessing.Messaging.RabbitMq.Topology;

public static class RabbitMqTopology
{
    public const string Exchange = "document.processing";
    public const string DeadLetterExchange = "document.processing.dlx";
    public const string RequestQueue = "document.conversion.request";
    public const string DeadLetterQueue = "document.conversion.dead-letter";
    public const string RequestedRoutingKey = "conversion.requested";
    public const string CompletedRoutingKey = "conversion.completed";
    public const string FailedRoutingKey = "conversion.failed";
    public const string DeadLetterRoutingKey = "conversion.dead-letter";
}
