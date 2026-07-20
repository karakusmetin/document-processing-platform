namespace DocumentProcessing.Contracts.Messages;

public sealed record ConversionRequested
{
    public const int CurrentSchemaVersion = 1;

    public required Guid JobId { get; init; }
    public required string CorrelationId { get; init; }
    public required string SourceReference { get; init; }
    public required string SourceFileName { get; init; }
    public string Profile { get; init; } = "display-copy";
    public int Attempt { get; init; } = 1;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
