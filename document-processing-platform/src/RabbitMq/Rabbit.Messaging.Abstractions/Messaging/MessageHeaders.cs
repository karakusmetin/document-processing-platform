namespace Rabbit.Messaging.Abstractions;

public static class MessageHeaders
{
    public const string MessageId = "message-id";
    public const string CorrelationId = "correlation-id";
    public const string CausationId = "causation-id";
    public const string MessageType = "message-type";
    public const string MessageVersion = "message-version";
    public const string Producer = "producer";
    public const string Attempt = "attempt";
    public const string CreatedAtUtc = "created-at-utc";
}