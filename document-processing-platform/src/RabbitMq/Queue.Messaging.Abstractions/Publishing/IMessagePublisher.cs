namespace Queue.Messaging.Abstractions;

public interface IMessagePublisher
{
    Task PublishAsync<TMessage>(TMessage message, MessagePublishContext context, CancellationToken cancellationToken = default);
}