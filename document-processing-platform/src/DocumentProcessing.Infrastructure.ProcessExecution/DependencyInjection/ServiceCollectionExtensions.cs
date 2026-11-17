using Microsoft.Extensions.DependencyInjection;

namespace DocumentProcessing.Infrastructure.ProcessExecution.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProcessExecution(this IServiceCollection services)
    {
        services.AddSingleton<IExternalProcessRunner, ExternalProcessRunner>();
        return services;
    }
}
