using DocumentProcessing.Converters.Text.Options;
using DocumentProcessing.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentProcessing.Converters.Text.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTextConversion(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TextConversionOptions>()
            .Bind(configuration.GetSection(TextConversionOptions.SectionName))
            .Validate(
                o => string.Equals(o.PageSize, "A4", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(o.PageSize, "LETTER", StringComparison.OrdinalIgnoreCase),
                "PageSize must be A4 or LETTER")
            .Validate(o => !string.IsNullOrWhiteSpace(o.FontFamily), "FontFamily is required")
            .Validate(o => o.FontSize > 0, "FontSize must be positive")
            .Validate(o => o.MarginPoints >= 0 && o.MarginPoints < 250, "MarginPoints must be between 0 and 250")
            .ValidateOnStart();

        services.AddSingleton<IConverterProvider, TextConverter>();
        return services;
    }
}
