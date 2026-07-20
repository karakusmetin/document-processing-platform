namespace DocumentProcessing.Core.Models;

public sealed record ConversionRequest
{
    public required Guid JobId { get; init; }
    public required string CorrelationId { get; init; }
    public required string SourceReference { get; init; }
    public required string SourceFileName { get; init; }
    public required string Profile { get; init; }
    public required int Attempt { get; init; }
}
