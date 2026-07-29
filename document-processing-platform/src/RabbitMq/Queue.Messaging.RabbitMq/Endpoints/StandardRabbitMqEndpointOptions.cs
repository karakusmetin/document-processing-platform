using Queue.Messaging.RabbitMq.Configuration;
using Queue.Messaging.RabbitMq.Compatibility;

namespace Queue.Messaging.RabbitMq.Endpoints.Standard;

/// <summary>
/// Standart bir RabbitMQ endpoint'in mesaj sözleşmesini,
/// fiziksel topology isimlerini ve endpoint bazlı runtime
/// override değerlerini tanımlar.
/// </summary>
public sealed class StandardRabbitMqEndpointOptions
{
    public const string DefaultMessageVersion =
        "1.0";

    public StandardRabbitMqEndpointOptions(
        string endpointName)
    {
        EndpointName =
            Guard.NotNullOrWhiteSpace(
                    endpointName,
                    nameof(endpointName))
                .Trim();

        Names =
            StandardRabbitMqEndpointNameBuilder.Build(
                EndpointName);
    }

    /// <summary>
    /// Endpoint'in mantıksal ve benzersiz adıdır.
    /// </summary>
    public string EndpointName { get; }

    /// <summary>
    /// Envelope içerisinde taşınan kararlı mesaj sözleşmesi
    /// adıdır.
    ///
    /// Generic registration metodu tarafından varsayılan
    /// olarak CLR type adıyla doldurulacaktır.
    /// </summary>
    public string MessageType { get; set; } =
        string.Empty;

    /// <summary>
    /// Mesaj sözleşmesi sürümüdür.
    /// </summary>
    public string MessageVersion { get; set; } =
        DefaultMessageVersion;

    /// <summary>
    /// Fiziksel RabbitMQ isimlerini içerir.
    ///
    /// Varsayılan değerler endpoint adından convention ile
    /// üretilir. Gerekirse uygulama tarafından değiştirilebilir.
    /// </summary>
    public StandardRabbitMqEndpointNames Names { get; set; }

    /// <summary>
    /// Topology definition çalışma sırasıdır.
    /// Küçük değerler önce çalışır.
    /// </summary>
    public int TopologyOrder { get; set; }

    /*
     * Aşağıdaki nullable property'lerde null:
     *
     * "Endpoint özelinde override yok, global RabbitMQ
     * configuration değerini kullan."
     *
     * anlamına gelir.
     */

    public ushort? PrefetchCount { get; set; }

    public int? ConcurrentConsumerCount { get; set; }

    public TimeSpan? ShutdownTimeout { get; set; }

    public RabbitMqQueueType? QueueType { get; set; }

    public int? MaximumAttempts { get; set; }

    public int[]? DelaySeconds { get; set; }
}