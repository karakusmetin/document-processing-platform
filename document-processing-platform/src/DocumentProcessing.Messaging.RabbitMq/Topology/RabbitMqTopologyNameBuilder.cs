namespace DocumentProcessing.Messaging.RabbitMq.Topology;

public static class RabbitMqTopologyNameBuilder
{
    public static string GetRetryQueueName(
        string prefix,
        int delaySeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

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