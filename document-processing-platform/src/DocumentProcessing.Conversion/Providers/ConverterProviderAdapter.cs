using DocumentProcessing.Contracts.Converter;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Errors;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Conversion.Providers;

public sealed class ConverterProviderAdapter(
    IEnumerable<IConverterProvider> converters) : IConversionProvider
{
    private readonly IReadOnlyList<IConverterProvider> _converters = converters.ToArray();

    public string Name => "converter-provider-adapter";

    public bool CanHandle(FileFormat sourceFormat, string profile) =>
        TryGetDocumentType(sourceFormat, out DocumentType sourceType) &&
        _converters.Any(converter => converter.CanConvert(sourceType, DocumentType.Pdf));

    public async Task<ConversionProviderResult> ConvertAsync(
        Stream source,
        FileFormat sourceFormat,
        string profile,
        CancellationToken cancellationToken)
    {
        if (!TryGetDocumentType(sourceFormat, out DocumentType sourceType))
        {
            throw new ConverterOperationException(
                ConversionErrorCodes.UnsupportedFormat,
                $"No converter mapping exists for format '{sourceFormat.Name}'.");
        }

        IConverterProvider? converter = _converters.FirstOrDefault(
            candidate => candidate.CanConvert(sourceType, DocumentType.Pdf));

        if (converter is null)
        {
            throw new ConverterOperationException(
                ConversionErrorCodes.ProviderUnavailable,
                $"No converter provider is available for '{sourceFormat.Name}'.");
        }

        using MemoryStream input = new();
        await source.CopyToAsync(input, cancellationToken);
        if (input.Length == 0)
        {
            throw new ConverterOperationException(
                ConversionErrorCodes.SourceFileCorrupted,
                "Source document is empty.");
        }

        ConverterResult result = await converter.ConvertAsync(
            new ConverterRequest
            {
                Content = input.ToArray(),
                SourceType = sourceType,
                TargetType = DocumentType.Pdf
            },
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (!result.IsSuccessful)
        {
            throw new ConverterOperationException(
                result.ErrorCode ?? ConversionErrorCodes.Unexpected,
                result.ErrorMessage ?? "Converter provider failed.");
        }

        if (result.DocumentType != DocumentType.Pdf || result.Content is null)
        {
            throw new ConverterOperationException(
                ConversionErrorCodes.ConversionOutputInvalid,
                "Converter provider returned an invalid result contract.");
        }

        return new ConversionProviderResult(
            new MemoryStream(result.Content, writable: false),
            converter.Name);
    }

    private static bool TryGetDocumentType(FileFormat format, out DocumentType documentType)
    {
        foreach ((DocumentType type, FileFormat registeredFormat) in FileFormat.ConvertibleFormats)
        {
            if (registeredFormat == format)
            {
                documentType = type;
                return true;
            }
        }

        documentType = default;
        return false;
    }
}
