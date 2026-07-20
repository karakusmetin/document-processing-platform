using System.Text.Json;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Messaging.RabbitMq.Connection;
using DocumentProcessing.Messaging.RabbitMq.Topology;
using RabbitMQ.Client;

namespace DocumentProcessing.Messaging.RabbitMq.Services;

public sealed class RabbitMqEventPublisher(IRabbitMqConnectionProvider connectionProvider) : IIntegrationEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync<T>(T message, string routingKey, CancellationToken cancellationToken)
        where T : class
    {
        IConnection connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        await using IChannel channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        BasicProperties properties = new()
        {
            Persistent = true,
            ContentType = "application/json",
            Type = typeof(T).FullName,
            MessageId = Guid.NewGuid().ToString("N"),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(
            RabbitMqTopology.Exchange,
            routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}
