using RabbitMQ.Client;

namespace Queue.Messaging.RabbitMq.Channels;

public interface IRabbitMqChannelFactory
{
    Task<IChannel> CreateChannelAsync(RabbitMqChannelPurpose purpose, CancellationToken cancellationToken = default);
}