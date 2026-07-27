using Rabbit.Messaging.Abstractions;

namespace DocumentProcessing.Messaging.RabbitMq.Consuming;

/// <summary>
/// Belirli bir mesaj sözleşmesini işleyen uygulama handler'ı.
/// </summary>
/// <typeparam name="TMessage">
/// Envelope içindeki payload türü.
/// </typeparam>
public interface IRabbitMqMessageHandler<TMessage>
{
    Task<RabbitMqMessageHandlingResult> HandleAsync(
        MessageEnvelope<TMessage> envelope,
        RabbitMqDeliveryContext delivery,
        CancellationToken cancellationToken);
}