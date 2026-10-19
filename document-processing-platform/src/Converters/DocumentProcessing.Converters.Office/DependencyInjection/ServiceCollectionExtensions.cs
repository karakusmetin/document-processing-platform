using DocumentProcessing.Converters.Office.Options;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Infrastructure.ProcessExecution.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentProcessing.Converters.Office.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOfficeConversion(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LibreOfficeOptions>()
            .Bind(configuration.GetSection(LibreOfficeOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.RuntimeRelativePath), "RuntimeRelativePath is required")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ExecutableRelativePath), "ExecutableRelativePath is required")
            .Validate(o => !Path.IsPathFullyQualified(o.ExecutableRelativePath), "ExecutableRelativePath must be relative to RuntimeRelativePath")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ProfileTemplateRelativePath), "ProfileTemplateRelativePath is required")
            .Validate(o => !Path.IsPathFullyQualified(o.ProfileTemplateRelativePath), "ProfileTemplateRelativePath must be relative to RuntimeRelativePath")
            .Validate(o => o.TimeoutSeconds > 0, "TimeoutSeconds must be positive")
            .ValidateOnStart();

        services.AddProcessExecution();
        services.AddSingleton<IConverterProvider, OfficeConverter>();
        return services;
    }
}
