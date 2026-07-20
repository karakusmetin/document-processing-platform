using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Core.Abstractions;

public interface IArtifactStore
{
    Task<ArtifactWriteResult> SaveAsync(
        Guid jobId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid jobId, string fileName, CancellationToken cancellationToken);
}
