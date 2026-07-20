using RabbitMQ.Client;

namespace DocumentProcessing.Messaging.RabbitMq.Connection;

public interface IRabbitMqConnectionProvider : IAsyncDisposable
{
    bool IsConnected { get; }

    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
}