using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Storage.Options;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Storage.Services;

public sealed class AliasFileReferenceResolver(IOptions<FileStorageOptions> options) : IDocumentSource
{
    private readonly FileStorageOptions _options = options.Value;

    public Task<Stream> OpenReadAsync(string sourceReference, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);

        Uri uri = new(sourceReference, UriKind.Absolute);
        if (!_options.Aliases.TryGetValue(uri.Scheme, out string? root))
        {
            throw new InvalidOperationException($"Unknown storage alias: {uri.Scheme}");
        }

        string relative = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string fullRoot = Path.GetFullPath(root);
        string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relative));

        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Source reference escaped its configured storage root.");
        }

        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult(stream);
    }
}
