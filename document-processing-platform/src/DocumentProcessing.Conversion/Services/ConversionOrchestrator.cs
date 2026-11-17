using DocumentProcessing.Conversion.Errors;
using DocumentProcessing.Conversion.Validation;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Errors;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Conversion.Services;

public sealed class ConversionOrchestrator(
    IDocumentSource documentSource,
    IFileFormatDetector formatDetector,
    IArtifactStore artifactStore,
    IConversionRequestValidator requestValidator,
    IConversionProviderResolver providerResolver,
    IConversionOutputValidator outputValidator,
    IConversionFailureMapper failureMapper) : IConversionOrchestrator
{
    private const string OutputArtifactName = "display.pdf";

    public async Task<ConversionExecutionResult> ExecuteAsync(
        ConversionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ConversionStage stage = ConversionStage.RequestValidation;

        ConversionRequestValidationResult validation = requestValidator.Validate(request);
        if (!validation.IsValid)
        {
            return failureMapper.Create(validation.Error!, stage);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            stage = ConversionStage.SourceResolve;
            await using Stream source = await documentSource.OpenReadAsync(
                request.SourceReference,
                cancellationToken);

            stage = ConversionStage.FormatDetection;
            FileFormat format = await formatDetector.DetectAsync(
                source,
                request.SourceFileName,
                cancellationToken);
            Reset(source);

            stage = ConversionStage.ProviderSelection;
            IConversionProvider? provider = providerResolver.Resolve(format, request.Profile);
            if (provider is null)
            {
                return failureMapper.Create(
                    ConversionErrorCatalog.Get(ConversionErrorCodes.UnsupportedFormat),
                    stage);
            }

            stage = ConversionStage.Conversion;
            ConversionProviderResult conversion = await provider.ConvertAsync(
                source,
                format,
                request.Profile,
                cancellationToken);
            await using Stream converted = conversion.Content;

            stage = ConversionStage.OutputValidation;
            await outputValidator.ValidateAsync(converted, cancellationToken);

            stage = ConversionStage.ArtifactWrite;
            ArtifactWriteResult artifact = await artifactStore.SaveAsync(
                request.JobId,
                OutputArtifactName,
                converted,
                cancellationToken);

            return ConversionExecutionResult.Success(
                artifact.Reference,
                artifact.Size,
                artifact.Sha256,
                conversion.ProviderName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return failureMapper.FromException(exception, stage);
        }
    }

    private static void Reset(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
    }
}
