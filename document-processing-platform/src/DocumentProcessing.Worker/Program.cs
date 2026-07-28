using DocumentProcessing.Conversion.DependencyInjection;
using Queue.Messaging.RabbitMq.DependencyInjection;
using DocumentProcessing.Storage.DependencyInjection;
using DocumentProcessing.Worker.DependencyInjection;

HostApplicationBuilder builder =
    Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(
    options =>
        options.ServiceName =
            "DocumentProcessingWorker");

builder.Services.AddRabbitMqMessaging(builder.Configuration).AddRabbitMqTopologyInitialization();

builder.Services.AddFileStorage(builder.Configuration);

builder.Services.AddDocumentConversion();

builder.Services.AddConversionMessaging(builder.Configuration);

await builder
    .Build()
    .RunAsync();