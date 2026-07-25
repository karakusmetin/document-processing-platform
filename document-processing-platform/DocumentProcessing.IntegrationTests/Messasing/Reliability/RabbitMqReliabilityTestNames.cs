using DocumentProcessing.Messaging.RabbitMq.Topology;

namespace DocumentProcessing.IntegrationTests
    .Messaging.Reliability;

internal sealed record RabbitMqReliabilityTestNames
{
    public const int RetryDelaySeconds =
        1;

    public required string DefinitionName { get; init; }

    public required string CommandExchange { get; init; }

    public required string RetryExchange { get; init; }

    public required string DeadLetterExchange { get; init; }

    public required string RequestQueue { get; init; }

    public required string RetryQueuePrefix { get; init; }

    public required string RetryQueue { get; init; }

    public required string DeadLetterQueue { get; init; }

    public required string RequestedRoutingKey { get; init; }

    public required string RetryRoutingKeyPrefix { get; init; }

    public required string RetryRoutingKey { get; init; }

    public required string DeadLetterRoutingKey { get; init; }

    public static RabbitMqReliabilityTestNames Create()
    {
        string suffix =
            Guid.NewGuid().ToString("N");

        string retryQueuePrefix =
            $"integration.reliability.retry.queue.{suffix}";

        string retryRoutingKeyPrefix =
            $"integration.reliability.retry.route.{suffix}";

        return new RabbitMqReliabilityTestNames
        {
            DefinitionName =
                $"integration.reliability.{suffix}",

            CommandExchange =
                $"integration.reliability.commands.{suffix}",

            RetryExchange =
                $"integration.reliability.retry.{suffix}",

            DeadLetterExchange =
                $"integration.reliability.dead-letter.{suffix}",

            RequestQueue =
                $"integration.reliability.requests.{suffix}",

            RetryQueuePrefix =
                retryQueuePrefix,

            RetryQueue =
                RabbitMqTopologyNameBuilder
                    .GetRetryQueueName(
                        retryQueuePrefix,
                        RetryDelaySeconds),

            DeadLetterQueue =
                $"integration.reliability.dlq.{suffix}",

            RequestedRoutingKey =
                $"integration.reliability.requested.{suffix}",

            RetryRoutingKeyPrefix =
                retryRoutingKeyPrefix,

            RetryRoutingKey =
                RabbitMqTopologyNameBuilder
                    .GetRetryRoutingKey(
                        retryRoutingKeyPrefix,
                        RetryDelaySeconds),

            DeadLetterRoutingKey =
                $"integration.reliability.dead.{suffix}"
        };
    }
}