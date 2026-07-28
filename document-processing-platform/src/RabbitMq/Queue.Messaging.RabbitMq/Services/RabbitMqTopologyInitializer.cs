using Queue.Messaging.RabbitMq.Connection;
using Queue.Messaging.RabbitMq.Topology;
using RabbitMQ.Client;

namespace Queue.Messaging.RabbitMq.Services;

public sealed class RabbitMqTopologyInitializer(IRabbitMqConnectionProvider connectionProvider)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        IConnection connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(RabbitMqTopology.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(RabbitMqTopology.DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        Dictionary<string, object?> arguments = new()
        {
            ["x-dead-letter-exchange"] = RabbitMqTopology.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = RabbitMqTopology.DeadLetterRoutingKey
        };

        await channel.QueueDeclareAsync(RabbitMqTopology.RequestQueue, durable: true, exclusive: false, autoDelete: false, arguments, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(RabbitMqTopology.RequestQueue, RabbitMqTopology.Exchange, RabbitMqTopology.RequestedRoutingKey, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(RabbitMqTopology.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(RabbitMqTopology.DeadLetterQueue, RabbitMqTopology.DeadLetterExchange, RabbitMqTopology.DeadLetterRoutingKey, cancellationToken: cancellationToken);
    }
}
