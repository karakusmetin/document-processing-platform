using Queue.Messaging.RabbitMq.Compatibility;

namespace Queue.Messaging.RabbitMq.Endpoints.Standard;

/// <summary>
/// Standard endpoint duplicate kayıtlarının IServiceCollection
/// üzerinde erken tespit edilmesini sağlar.
/// </summary>
internal sealed class StandardRabbitMqEndpointMarker
{
    public StandardRabbitMqEndpointMarker(
        string endpointName,
        Type messageClrType)
    {
        EndpointName =
            Guard.NotNullOrWhiteSpace(
                    endpointName,
                    nameof(endpointName))
                .Trim();

        Guard.NotNull(
            messageClrType,
            nameof(messageClrType));

        MessageClrType =
            messageClrType;
    }

    public string EndpointName { get; }

    public Type MessageClrType { get; }
}