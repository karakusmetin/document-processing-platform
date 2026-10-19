using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Conversion.Services;

public sealed class ConversionProviderResolver(IEnumerable<IConversionProvider> providers)
    : IConversionProviderResolver
{
    private readonly IReadOnlyList<IConversionProvider> _providers = providers.ToArray();

    public IConversionProvider? Resolve(FileFormat sourceFormat, string profile) =>
        _providers.FirstOrDefault(provider => provider.CanHandle(sourceFormat, profile));
}
