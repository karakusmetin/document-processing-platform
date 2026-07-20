using DocumentProcessing.Conversion.Detection;
using DocumentProcessing.Conversion.Providers;
using DocumentProcessing.Conversion.Services;
using DocumentProcessing.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentProcessing.Conversion.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentConversion(this IServiceCollection services)
    {
        services.AddSingleton<IFileFormatDetector, BasicFileFormatDetector>();
        services.AddSingleton<IConversionProvider, PdfPassThroughProvider>();
        services.AddSingleton<IConversionOrchestrator, ConversionOrchestrator>();
        return services;
    }
}
