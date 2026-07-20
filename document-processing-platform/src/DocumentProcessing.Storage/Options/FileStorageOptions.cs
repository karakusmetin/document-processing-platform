namespace DocumentProcessing.Storage.Options;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";
    public Dictionary<string, string> Aliases { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public required string ArtifactRoot { get; init; }
}
