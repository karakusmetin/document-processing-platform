using DocumentProcessing.Contracts.Messages;

namespace DocumentProcessing.Client;

public interface IDocumentProcessingClient
{
    Task<Guid> SubmitAsync(ConversionRequested request, CancellationToken cancellationToken = default);
}
