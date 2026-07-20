using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Errors;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Conversion.Services;

public sealed class ConversionOrchestrator(
    IDocumentSource documentSource,
    IFileFormatDetector formatDetector,
    IEnumerable<IConversionProvider> providers,
    IArtifactStore artifactStore) : IConversionOrchestrator
{
    public async Task<ConversionExecutionResult> ExecuteAsync(
        ConversionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using Stream source = await documentSource.OpenReadAsync(request.SourceReference, cancellationToken);
            FileFormat format = await formatDetector.DetectAsync(source, request.SourceFileName, cancellationToken);
            IConversionProvider? provider = providers.FirstOrDefault(x => x.CanHandle(format, request.Profile));

            if (provider is null)
            {
                return ConversionExecutionResult.Failure(
                    ConversionErrorCodes.UnsupportedFormat,
                    $"No provider supports detected format '{format.Name}'.",
                    retryable: false,
                    failedStage: "provider-selection");
            }

            if (source.CanSeek)
            {
                source.Position = 0;
            }

            await using Stream converted = await provider.ConvertAsync(source, format, request.Profile, cancellationToken);
            ArtifactWriteResult artifact = await artifactStore.SaveAsync(
                request.JobId,
                "display.pdf",
                converted,
                cancellationToken);

            return ConversionExecutionResult.Success(
                artifact.Reference,
                artifact.Size,
                artifact.Sha256,
                provider.Name);
        }
        catch (FileNotFoundException ex)
        {
            return ConversionExecutionResult.Failure(
                ConversionErrorCodes.SourceNotFound,
                ex.Message,
                retryable: false,
                failedStage: "source-resolve");
        }
        catch (UnauthorizedAccessException ex)
        {
            return ConversionExecutionResult.Failure(
                ConversionErrorCodes.SourceAccessDenied,
                ex.Message,
                retryable: false,
                failedStage: "source-resolve");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ConversionExecutionResult.Failure(
                ConversionErrorCodes.Unexpected,
                ex.Message,
                retryable: true,
                failedStage: "orchestration");
        }
    }
}
