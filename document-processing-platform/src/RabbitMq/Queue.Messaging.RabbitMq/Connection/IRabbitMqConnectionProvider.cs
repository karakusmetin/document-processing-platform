using RabbitMQ.Client;

namespace Queue.Messaging.RabbitMq.Connection;

public interface IRabbitMqConnectionProvider : IAsyncDisposable
{
    bool IsConnected { get; }

    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
}