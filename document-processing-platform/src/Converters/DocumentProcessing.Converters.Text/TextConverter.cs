using DocumentProcessing.Contracts.Converter;
using DocumentProcessing.Converters.Text.Options;
using DocumentProcessing.Converters.Text.Rendering;
using DocumentProcessing.Core.Providers;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Converters.Text
{
    public sealed class TextConverter(IOptions<TextConversionOptions> options) : ConverterProviderBase
    {
        private readonly TextConversionOptions _options = options.Value;

        public override string Name => "Text Converter";

        public override bool CanConvert(DocumentType sourceType, DocumentType targetType)
        {
            bool supportedSource = sourceType is DocumentType.Txt or DocumentType.Csv;
            return supportedSource && targetType == DocumentType.Pdf;
        }

        protected override Task<byte[]> ConvertCoreAsync(ConverterRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] result = request.SourceType switch
            {
                DocumentType.Txt => new TextPdfRenderer(_options).Render(request.Content),
                DocumentType.Csv => new CsvPdfRenderer(_options).Render(request.Content),
                _ => throw new NotSupportedException($"Desteklenmeyen metin tipi: {request.SourceType}")
            };

            return Task.FromResult(result);
        }
    }
}
