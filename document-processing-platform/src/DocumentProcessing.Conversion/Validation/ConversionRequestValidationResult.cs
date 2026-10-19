using DocumentProcessing.Core.Errors;

namespace DocumentProcessing.Conversion.Validation;

public readonly record struct ConversionRequestValidationResult(
    bool IsValid,
    ConversionErrorDefinition? Error)
{
    public static ConversionRequestValidationResult Success() => new(true, null);

    public static ConversionRequestValidationResult Failure(ConversionErrorDefinition error) =>
        new(false, error);
}
