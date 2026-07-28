using Queue.Messaging.RabbitMq.Configuration;

namespace Queue.Messaging.RabbitMq.Topology;

/// <summary>
/// Uygulama topology tanımlarının RabbitMQ.Client channel
/// nesnesine doğrudan erişmeden exchange, queue ve binding
/// oluşturmasını sağlar.
/// </summary>
public interface IRabbitMqTopologyBuilder
{
    Task DeclareExchangeAsync(
        string name,
        string type,
        bool durable = true,
        bool autoDelete = false,
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default);

    Task DeclareQueueAsync(
        string name,
        RabbitMqQueueType? queueType = null,
        bool durable = true,
        bool exclusive = false,
        bool autoDelete = false,
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default);

    Task BindQueueAsync(
        string queue,
        string exchange,
        string routingKey,
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default);
}