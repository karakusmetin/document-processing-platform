using Queue.Messaging.RabbitMq.Compatibility;
using Queue.Messaging.RabbitMq.Configuration;

namespace Queue.Messaging.RabbitMq.Endpoints.Standard;

/// <summary>
/// Standard endpoint options değerlerinin registration
/// sonrasında değiştirilemeyen internal kopyasıdır.
/// </summary>
internal sealed class
    StandardRabbitMqEndpointRegistration<TMessage>
{
    public StandardRabbitMqEndpointRegistration(
        StandardRabbitMqEndpointOptions options)
    {
        Guard.NotNull(
            options,
            nameof(options));

        StandardRabbitMqEndpointNames? names =
            options.Names;

        if (names is null)
        {
            throw new ArgumentException(
                "Standard RabbitMQ endpoint names are required.",
                nameof(options));
        }

        EndpointName =
            options.EndpointName;

        MessageType =
            options.MessageType;

        MessageVersion =
            options.MessageVersion;

        TopologyOrder =
            options.TopologyOrder;

        PrefetchCount =
            options.PrefetchCount;

        ConcurrentConsumerCount =
            options.ConcurrentConsumerCount;

        ShutdownTimeout =
            options.ShutdownTimeout;

        QueueType =
            options.QueueType;

        MaximumAttempts =
            options.MaximumAttempts;

        /*
         * Caller'ın verdiği array referansını doğrudan
         * saklamıyoruz.
         */
        DelaySeconds =
            options.DelaySeconds?
                .ToArray();

        /*
         * Public Names nesnesi mutable olduğu için onun da
         * bağımsız bir kopyasını oluşturuyoruz.
         */
        Names =
            new StandardRabbitMqEndpointNames
            {
                ExchangeName =
                    names.ExchangeName,

                QueueName =
                    names.QueueName,

                RoutingKey =
                    names.RoutingKey,

                RetryExchangeName =
                    names.RetryExchangeName,

                RetryQueueNamePrefix =
                    names.RetryQueueNamePrefix,

                RetryRoutingKeyPrefix =
                    names.RetryRoutingKeyPrefix,

                DeadLetterExchangeName =
                    names.DeadLetterExchangeName,

                DeadLetterQueueName =
                    names.DeadLetterQueueName,

                DeadLetterRoutingKey =
                    names.DeadLetterRoutingKey,

                ConsumerTagPrefix =
                    names.ConsumerTagPrefix
            };
    }

    public string EndpointName { get; }

    public string MessageType { get; }

    public string MessageVersion { get; }

    public StandardRabbitMqEndpointNames Names { get; }

    public int TopologyOrder { get; }

    public ushort? PrefetchCount { get; }

    public int? ConcurrentConsumerCount { get; }

    public TimeSpan? ShutdownTimeout { get; }

    public RabbitMqQueueType? QueueType { get; }

    public int? MaximumAttempts { get; }

    public int[]? DelaySeconds { get; }
}