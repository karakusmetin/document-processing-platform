using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Conversion.Providers;

public sealed class PdfPassThroughProvider : IConversionProvider
{
    public string Name => "pdf-pass-through";

    public bool CanHandle(FileFormat sourceFormat, string profile) => sourceFormat == FileFormat.Pdf;

    public async Task<Stream> ConvertAsync(
        Stream source,
        FileFormat sourceFormat,
        string profile,
        CancellationToken cancellationToken)
    {
        MemoryStream output = new();
        await source.CopyToAsync(output, cancellationToken);
        output.Position = 0;
        return output;
    }
}
