using Queue.Messaging.Abstractions;

namespace Queue.Messaging.RabbitMq.Serialization;

public interface IMessageSerializer
{
    ReadOnlyMemory<byte> Serialize<TMessage>(MessageEnvelope<TMessage> envelope);
    MessageEnvelope<TMessage> Deserialize<TMessage>(ReadOnlyMemory<byte> body);
}