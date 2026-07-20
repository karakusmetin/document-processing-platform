using System.Security.Cryptography;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Storage.Options;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Storage.Services;

public sealed class LocalArtifactStore(IOptions<FileStorageOptions> options) : IArtifactStore
{
    private readonly FileStorageOptions _options = options.Value;

    public Task<bool> ExistsAsync(Guid jobId, string fileName, CancellationToken cancellationToken)
    {
        string path = BuildPath(jobId, fileName);
        return Task.FromResult(File.Exists(path));
    }

    public async Task<ArtifactWriteResult> SaveAsync(
        Guid jobId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        string path = BuildPath(jobId, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string tempPath = path + ".tmp";
        await using FileStream output = new(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024];
        long size = 0;
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            hash.AppendData(buffer, 0, read);
            size += read;
        }

        await output.FlushAsync(cancellationToken);
        output.Close();
        File.Move(tempPath, path, overwrite: true);

        string relative = Path.GetRelativePath(_options.ArtifactRoot, path).Replace('\\', '/');
        return new ArtifactWriteResult(
            $"artifact://{relative}",
            size,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    private string BuildPath(Guid jobId, string fileName)
    {
        string safeName = Path.GetFileName(fileName);
        return Path.Combine(_options.ArtifactRoot, jobId.ToString("N"), safeName);
    }
}
