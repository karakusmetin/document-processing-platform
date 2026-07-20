using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Storage.Options;
using DocumentProcessing.Storage.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentProcessing.Storage.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ArtifactRoot), "ArtifactRoot is required")
            .ValidateOnStart();

        services.AddSingleton<IDocumentSource, AliasFileReferenceResolver>();
        services.AddSingleton<IArtifactStore, LocalArtifactStore>();
        return services;
    }
}
