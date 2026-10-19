namespace DocumentProcessing.Core.Models;

public sealed record ConversionProviderResult(
    Stream Content,
    string ProviderName);
