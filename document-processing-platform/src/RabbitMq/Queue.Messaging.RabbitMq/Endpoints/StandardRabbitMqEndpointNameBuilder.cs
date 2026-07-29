using Queue.Messaging.RabbitMq.Compatibility;

namespace Queue.Messaging.RabbitMq.Endpoints.Standard;

internal static class StandardRabbitMqEndpointNameBuilder
{
    public static StandardRabbitMqEndpointNames Build(
        string endpointName)
    {
        string normalizedEndpointName =
            Guard.NotNullOrWhiteSpace(
                    endpointName,
                    nameof(endpointName))
                .Trim();

        return new StandardRabbitMqEndpointNames
        {
            ExchangeName = $"{normalizedEndpointName}.exchange",

            QueueName = $"{normalizedEndpointName}.queue",

            /*
             * Ana routing key olarak endpoint adını kullanıyoruz.
             *
             * İstenirse endpoint registration sırasında
             * override edilebilir.
             */
            RoutingKey = normalizedEndpointName,

            RetryExchangeName = $"{normalizedEndpointName}.retry.exchange",

            /*
             * RabbitMqTopologyNameBuilder daha sonra delay
             * değerini bu prefix'e ekleyecek.
             *
             * Örnek:
             * my-endpoint.retry.queue.10s
             */
            RetryQueueNamePrefix = $"{normalizedEndpointName}.retry.queue",

            /*
             * Örnek:
             * my-endpoint.retry.10s
             */
            RetryRoutingKeyPrefix = $"{normalizedEndpointName}.retry",

            DeadLetterExchangeName = $"{normalizedEndpointName}.dead-letter.exchange",

            DeadLetterQueueName = $"{normalizedEndpointName}.dead-letter.queue",

            DeadLetterRoutingKey = $"{normalizedEndpointName}.dead-letter",

            ConsumerTagPrefix = normalizedEndpointName
        };
    }
}