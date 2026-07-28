namespace DocumentProcessing.IntegrationTests
    .Messaging.PublishConsume;

internal sealed record RabbitMqIntegrationTestNames
{
    public required string DefinitionName { get; init; }

    public required string ExchangeName { get; init; }

    public required string QueueName { get; init; }

    public required string RoutingKey { get; init; }

    public static RabbitMqIntegrationTestNames Create()
    {
        string suffix =
            Guid.NewGuid().ToString("N");

        return new RabbitMqIntegrationTestNames
        {
            DefinitionName =
                $"integration.publish-consume.{suffix}",

            ExchangeName =
                $"integration.publish-consume.commands.{suffix}",

            QueueName =
                $"integration.publish-consume.requests.{suffix}",

            RoutingKey =
                $"integration.publish-consume.requested.{suffix}"
        };
    }
}