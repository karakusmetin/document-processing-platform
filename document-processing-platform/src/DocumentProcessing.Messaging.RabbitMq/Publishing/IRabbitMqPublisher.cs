using DocumentProcessing.Contracts.Messaging;

namespace DocumentProcessing.Messaging.RabbitMq.Publishing;

internal interface IRabbitMqPublisher : IAsyncDisposable
{
    Task PublishAsync<TMessage>(
        MessageEnvelope<TMessage> envelope,
        RabbitMqPublishDestination destination,
        CancellationToken cancellationToken = default);
}