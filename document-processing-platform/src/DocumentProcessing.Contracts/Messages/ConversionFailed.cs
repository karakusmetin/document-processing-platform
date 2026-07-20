namespace DocumentProcessing.Contracts.Messages;

public sealed record ConversionFailed
{
    public required Guid JobId { get; init; }
    public required string CorrelationId { get; init; }
    public required string ErrorCode { get; init; }
    public required string Message { get; init; }
    public required bool Retryable { get; init; }
    public required string FailedStage { get; init; }
    public required int Attempt { get; init; }
    public string? DiagnosticId { get; init; }
    public DateTimeOffset FailedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
