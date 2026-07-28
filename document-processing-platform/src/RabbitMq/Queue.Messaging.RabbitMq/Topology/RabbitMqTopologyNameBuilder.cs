using Queue.Messaging.RabbitMq.Compatibility;

namespace Queue.Messaging.RabbitMq.Topology;

public static class RabbitMqTopologyNameBuilder
{
    public static string GetRetryQueueName(
        string prefix,
        int delaySeconds)
    {
        Guard.NotNullOrWhiteSpace(prefix, nameof(prefix));

        if (delaySeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delaySeconds),
                delaySeconds,
                "Retry delay must be greater than zero.");
        }

        return $"{prefix}.{delaySeconds}s";
    }

    public static string GetRetryRoutingKey(
        string prefix,
        int delaySeconds)
    {
        Guard.NotNullOrWhiteSpace(prefix, nameof(prefix));

        if (delaySeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delaySeconds),
                delaySeconds,
                "Retry delay must be greater than zero.");
        }

        return $"{prefix}.{delaySeconds}s";
    }
}