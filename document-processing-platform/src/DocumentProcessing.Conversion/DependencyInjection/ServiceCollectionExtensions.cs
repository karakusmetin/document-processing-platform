using DocumentProcessing.Conversion.Detection;
using DocumentProcessing.Conversion.Errors;
using DocumentProcessing.Conversion.Providers;
using DocumentProcessing.Conversion.Services;
using DocumentProcessing.Conversion.Validation;
using DocumentProcessing.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentProcessing.Conversion.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentConversion(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton<IFileFormatDetector, BasicFileFormatDetector>();
        services.AddSingleton<IConversionProvider, PdfPassThroughProvider>();
        services.AddSingleton<IConversionProvider, ConverterProviderAdapter>();
        services.AddSingleton<IConversionRequestValidator, ConversionRequestValidator>();
        services.AddSingleton<IConversionProviderResolver, ConversionProviderResolver>();
        services.AddSingleton<IConversionOutputValidator, PdfConversionOutputValidator>();
        services.AddSingleton<IConversionErrorMessageProvider, ResourceConversionErrorMessageProvider>();
        services.AddSingleton<IConversionFailureMapper, ConversionFailureMapper>();
        services.AddSingleton<IConversionOrchestrator, ConversionOrchestrator>();
        return services;
    }
}
