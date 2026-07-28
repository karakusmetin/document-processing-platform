namespace DocumentProcessing.Worker.Configuration;

internal sealed class ConversionRabbitMqTopologyOptions
{
    public const string SectionName =
        "RabbitMq:Topology";

    public string CommandExchange { get; init; } =
        string.Empty;

    public string EventExchange { get; init; } =
        string.Empty;

    public string RetryExchange { get; init; } =
        string.Empty;

    public string DeadLetterExchange { get; init; } =
        string.Empty;

    public string ConversionRequestQueue { get; init; } =
        string.Empty;

    public string ConversionResultQueue { get; init; } =
        string.Empty;

    public string ConversionDeadLetterQueue { get; init; } =
        string.Empty;

    public string ConversionRequestedRoutingKey { get; init; } =
        string.Empty;

    public string ConversionCompletedRoutingKey { get; init; } =
        string.Empty;

    public string ConversionFailedRoutingKey { get; init; } =
        string.Empty;

    public string ConversionDeadLetterRoutingKey { get; init; } =
        string.Empty;

    public string RetryQueuePrefix { get; init; } =
        string.Empty;

    public string RetryRoutingKeyPrefix { get; init; } =
        string.Empty;
}