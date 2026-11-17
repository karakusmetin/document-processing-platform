using DocumentProcessing.Core.Errors;

namespace DocumentProcessing.Conversion.Validation;

public sealed class PdfConversionOutputValidator : IConversionOutputValidator
{
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    public async Task ValidateAsync(Stream output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!output.CanRead || !output.CanSeek || output.Length < PdfSignature.Length)
        {
            throw InvalidOutput();
        }

        output.Position = 0;
        byte[] signature = new byte[PdfSignature.Length];
        int bytesRead = await output.ReadAsync(signature, cancellationToken);
        output.Position = 0;

        if (bytesRead != PdfSignature.Length || !signature.AsSpan().SequenceEqual(PdfSignature))
        {
            throw InvalidOutput();
        }
    }

    private static ConverterOperationException InvalidOutput() =>
        new(
            ConversionErrorCodes.ConversionOutputInvalid,
            "PDF signature validation failed.");
}
