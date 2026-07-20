using RabbitMQ.Client;

namespace DocumentProcessing.Messaging.RabbitMq.Channels;

public interface IRabbitMqChannelFactory
{
    Task<IChannel> CreateChannelAsync(RabbitMqChannelPurpose purpose, CancellationToken cancellationToken = default);
}