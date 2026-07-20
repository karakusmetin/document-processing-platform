namespace DocumentProcessing.Core.Abstractions;

public interface IDocumentSource
{
    Task<Stream> OpenReadAsync(string sourceReference, CancellationToken cancellationToken);
}
