namespace DocumentProcessing.Messaging.RabbitMq.Topology;

public interface IRabbitMqTopologyInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}