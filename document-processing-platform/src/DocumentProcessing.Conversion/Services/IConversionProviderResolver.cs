using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Conversion.Services;

public interface IConversionProviderResolver
{
    IConversionProvider? Resolve(FileFormat sourceFormat, string profile);
}
