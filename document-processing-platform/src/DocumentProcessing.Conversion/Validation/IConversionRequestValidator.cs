using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Conversion.Validation;

public interface IConversionRequestValidator
{
    ConversionRequestValidationResult Validate(ConversionRequest request);
}
