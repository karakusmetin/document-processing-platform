using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Core.Abstractions;

public interface IFileFormatDetector
{
    Task<FileFormat> DetectAsync(Stream source, string declaredFileName, CancellationToken cancellationToken);
}
