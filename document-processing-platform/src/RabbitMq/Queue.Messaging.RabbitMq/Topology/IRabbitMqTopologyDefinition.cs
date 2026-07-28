namespace Queue.Messaging.RabbitMq.Topology;

/// <summary>
/// Bir uygulamanın RabbitMQ exchange, queue ve binding
/// tanımlarını oluşturur.
/// </summary>
public interface IRabbitMqTopologyDefinition
{
    /// <summary>
    /// Loglama ve duplicate definition kontrolü için
    /// topology tanımının benzersiz adıdır.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Birden fazla topology tanımı olduğunda çalışma sırasıdır.
    /// Küçük değerler önce çalışır.
    /// </summary>
    int Order { get; }

    Task DeclareAsync(IRabbitMqTopologyBuilder builder,CancellationToken cancellationToken);
}