namespace DocumentProcessing.Core.Abstractions;

public interface IIntegrationEventPublisher
{
    Task PublishAsync<T>(T message, string routingKey, CancellationToken cancellationToken)
        where T : class;
}
