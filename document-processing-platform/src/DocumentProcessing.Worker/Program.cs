using DocumentProcessing.Conversion.DependencyInjection;
using DocumentProcessing.Messaging.RabbitMq.DependencyInjection;
using DocumentProcessing.Storage.DependencyInjection;
using DocumentProcessing.Worker.Consumers;
using DocumentProcessing.Worker.Consumers.Retry;
using DocumentProcessing.Worker;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "Document Processing Worker");
builder.Services.AddRabbitMqMessaging(builder.Configuration).AddRabbitMqTopologyInitialization();
builder.Services.AddFileStorage(builder.Configuration);
builder.Services.AddDocumentConversion();
builder.Services.AddHostedService<ConversionConsumerWorker>();
builder.Services.AddSingleton<IConversionRequestMessageHandler,ConversionRequestMessageHandler>();
builder.Services.AddHostedService<ConversionConsumerWorker>();
builder.Services.AddSingleton<IRetryDelayProvider, ConfiguredRetryDelayProvider>();

await builder.Build().RunAsync();
