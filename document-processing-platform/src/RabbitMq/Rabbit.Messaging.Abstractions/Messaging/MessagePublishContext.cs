namespace Rabbit.Messaging.Abstractions;

public sealed record MessagePublishContext
{
    public string? CorrelationId { get; init; }

    public string? CausationId { get; init; }

    public int Attempt { get; init; } = 1;

    public Guid? MessageId { get; init; }
}