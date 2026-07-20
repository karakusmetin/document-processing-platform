using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Core.Abstractions;

public interface IConversionProvider
{
    string Name { get; }
    bool CanHandle(FileFormat sourceFormat, string profile);
    Task<Stream> ConvertAsync(Stream source, FileFormat sourceFormat, string profile, CancellationToken cancellationToken);
}
