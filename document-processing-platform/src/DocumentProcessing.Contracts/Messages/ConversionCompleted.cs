namespace DocumentProcessing.Contracts.Messages;

public sealed record ConversionCompleted
{
    public required Guid JobId { get; init; }
    public required string CorrelationId { get; init; }
    public required string OutputReference { get; init; }
    public required string OutputFormat { get; init; }
    public required long OutputSize { get; init; }
    public required string OutputSha256 { get; init; }
    public int? PageCount { get; init; }
    public required string Provider { get; init; }
    public DateTimeOffset CompletedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
