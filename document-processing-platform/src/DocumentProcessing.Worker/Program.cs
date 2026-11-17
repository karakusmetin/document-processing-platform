using DocumentProcessing.Conversion.DependencyInjection;
using DocumentProcessing.Converters.Office.DependencyInjection;
using DocumentProcessing.Converters.Text.DependencyInjection;
using Queue.Messaging.RabbitMq.DependencyInjection;
using DocumentProcessing.Storage.DependencyInjection;
using DocumentProcessing.Worker.DependencyInjection;
using Microsoft.Extensions.Configuration;

HostApplicationBuilder builder =
    Host.CreateApplicationBuilder(args);

if (OperatingSystem.IsLinux())
{
    builder.Configuration.AddJsonFile("appsettings.Linux.json", optional: false, reloadOnChange: false);
}

builder.Services.AddWindowsService(
    options =>
        options.ServiceName =
            "DocumentProcessingWorker");

builder.Services.AddRabbitMqMessaging(builder.Configuration).AddRabbitMqTopologyInitialization();

builder.Services.AddFileStorage(builder.Configuration);

builder.Services.AddOfficeConversion(builder.Configuration);

builder.Services.AddTextConversion(builder.Configuration);

builder.Services.AddDocumentConversion();

builder.Services.AddConversionMessaging(builder.Configuration);

await builder
    .Build()
    .RunAsync();
