namespace Rabbit.Messaging.Abstractions;

public sealed record MessageEnvelope<TMessage>
{
    public required Guid MessageId { get; init; }

    public required string MessageType { get; init; }

    public required string MessageVersion { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required string Producer { get; init; }

    public string? CorrelationId { get; init; }

    public string? CausationId { get; init; }

    public int Attempt { get; init; } = 1;

    public required TMessage Payload { get; init; }

    public static MessageEnvelope<TMessage> Create(
        TMessage payload,
        string messageType,
        string messageVersion,
        string producer,
        string? correlationId = null,
        string? causationId = null,
        int attempt = 1,
        Guid? messageId = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(producer);

        if (attempt < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attempt),
                attempt,
                "Message attempt must be greater than zero.");
        }

        return new MessageEnvelope<TMessage>
        {
            MessageId = messageId ?? Guid.NewGuid(),
            MessageType = messageType,
            MessageVersion = messageVersion,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Producer = producer,
            CorrelationId = correlationId,
            CausationId = causationId,
            Attempt = attempt,
            Payload = payload
        };
    }
}