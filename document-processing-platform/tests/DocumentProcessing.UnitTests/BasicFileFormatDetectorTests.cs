using System.Text;
using DocumentProcessing.Conversion.Detection;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.UnitTests;

public sealed class BasicFileFormatDetectorTests
{
    [Fact]
    public async Task DetectAsync_WhenPdfHeaderExists_ReturnsPdf()
    {
        BasicFileFormatDetector detector = new();
        await using MemoryStream stream = new(Encoding.ASCII.GetBytes("%PDF-1.7\n"));

        FileFormat result = await detector.DetectAsync(stream, "sample.bin", CancellationToken.None);

        Assert.Equal(FileFormat.Pdf, result);
        Assert.Equal(0, stream.Position);
    }
}
