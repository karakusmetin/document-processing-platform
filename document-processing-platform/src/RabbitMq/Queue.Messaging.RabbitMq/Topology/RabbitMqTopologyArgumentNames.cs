namespace Queue.Messaging.RabbitMq.Topology;

public static class RabbitMqTopologyArgumentNames
{
    public const string QueueType =
        "x-queue-type";

    public const string MessageTtl =
        "x-message-ttl";

    public const string DeadLetterExchange =
        "x-dead-letter-exchange";

    public const string DeadLetterRoutingKey =
        "x-dead-letter-routing-key";
}