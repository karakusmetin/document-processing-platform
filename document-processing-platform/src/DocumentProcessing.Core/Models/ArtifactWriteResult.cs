namespace DocumentProcessing.Core.Models;

public sealed record ArtifactWriteResult(
    string Reference,
    long Size,
    string Sha256);
