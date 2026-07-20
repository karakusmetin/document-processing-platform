using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Conversion.Detection;

public sealed class BasicFileFormatDetector : IFileFormatDetector
{
    private static readonly byte[] PdfHeader = "%PDF-"u8.ToArray();

    public async Task<FileFormat> DetectAsync(Stream source, string declaredFileName, CancellationToken cancellationToken)
    {
        if (!source.CanSeek)
        {
            throw new InvalidOperationException("Format detection requires a seekable stream in the first vertical slice.");
        }

        long originalPosition = source.Position;
        byte[] header = new byte[PdfHeader.Length];
        int read = await source.ReadAsync(header, cancellationToken);
        source.Position = originalPosition;

        if (read == PdfHeader.Length && header.AsSpan().SequenceEqual(PdfHeader))
        {
            return FileFormat.Pdf;
        }

        return FileFormat.Unknown;
    }
}
