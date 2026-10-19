using DocumentProcessing.Core.Errors;
using DocumentProcessing.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentProcessing.Conversion.Errors;

public sealed class ConversionFailureMapper(
    ILogger<ConversionFailureMapper> logger,
    IConversionErrorMessageProvider messageProvider) : IConversionFailureMapper
{
    public ConversionExecutionResult Create(
        ConversionErrorDefinition error,
        ConversionStage stage) =>
        ConversionExecutionResult.Failure(
            error.Code,
            messageProvider.GetMessage(error),
            error.Retryable,
            stage.ToContractValue());

    public ConversionExecutionResult FromException(
        Exception exception,
        ConversionStage stage)
    {
        ArgumentNullException.ThrowIfNull(exception);

        logger.LogError(
            exception,
            "Document conversion failed at stage {ConversionStage}",
            stage.ToContractValue());

        string errorCode = exception switch
        {
            FileNotFoundException when stage == ConversionStage.SourceResolve =>
                ConversionErrorCodes.SourceNotFound,
            UnauthorizedAccessException when stage == ConversionStage.SourceResolve =>
                ConversionErrorCodes.SourceAccessDenied,
            ConverterOperationException converterException =>
                converterException.ErrorCode,
            IOException when stage is ConversionStage.SourceResolve or
                ConversionStage.FormatDetection =>
                ConversionErrorCodes.SourceReadFailed,
            IOException when stage == ConversionStage.Conversion =>
                ConversionErrorCodes.ConversionIoFailed,
            IOException when stage == ConversionStage.ArtifactWrite =>
                ConversionErrorCodes.ArtifactWriteFailed,
            _ => ConversionErrorCodes.Unexpected
        };

        return Create(ConversionErrorCatalog.Get(errorCode), stage);
    }
}
