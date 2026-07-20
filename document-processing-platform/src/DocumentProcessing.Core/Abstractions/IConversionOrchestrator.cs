using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Core.Abstractions;

public interface IConversionOrchestrator
{
    Task<ConversionExecutionResult> ExecuteAsync(ConversionRequest request, CancellationToken cancellationToken);
}
