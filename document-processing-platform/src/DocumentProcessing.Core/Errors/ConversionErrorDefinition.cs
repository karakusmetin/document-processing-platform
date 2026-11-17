namespace DocumentProcessing.Core.Errors;

public sealed record ConversionErrorDefinition(
    string Code,
    bool Retryable);
