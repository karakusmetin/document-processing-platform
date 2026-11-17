using DocumentProcessing.Core.Errors;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Conversion.Validation;

public sealed class ConversionRequestValidator : IConversionRequestValidator
{
    public ConversionRequestValidationResult Validate(ConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.JobId == Guid.Empty ||
               string.IsNullOrWhiteSpace(request.SourceReference) ||
               string.IsNullOrWhiteSpace(request.SourceFileName) ||
               string.IsNullOrWhiteSpace(request.Profile)
            ? ConversionRequestValidationResult.Failure(
                ConversionErrorCatalog.Get(ConversionErrorCodes.InvalidRequest))
            : ConversionRequestValidationResult.Success();
    }
}
