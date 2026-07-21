using DocumentProcessing.Contracts.Messaging;

namespace DocumentProcessing.Core.Abstractions;

public interface IMessagePublisher
{
    Task PublishAsync<TMessage>(TMessage message, MessagePublishContext context, CancellationToken cancellationToken = default);
}