namespace DocumentProcessing.Messaging.RabbitMq.Connection;

public sealed record RabbitMqConnectionState(
    bool IsConnected,
    string? Endpoint,
    DateTimeOffset? ConnectedAtUtc,
    DateTimeOffset? DisconnectedAtUtc,
    string? LastError);