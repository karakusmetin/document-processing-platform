namespace DocumentProcessing.Conversion.Validation;

public interface IConversionOutputValidator
{
    Task ValidateAsync(Stream output, CancellationToken cancellationToken);
}
