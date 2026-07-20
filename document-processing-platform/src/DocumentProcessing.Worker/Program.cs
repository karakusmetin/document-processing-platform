using DocumentProcessing.Conversion.DependencyInjection;
using DocumentProcessing.Messaging.RabbitMq.DependencyInjection;
using DocumentProcessing.Storage.DependencyInjection;
using DocumentProcessing.Worker;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "Document Processing Worker");
builder.Services.AddRabbitMqMessaging(builder.Configuration);
builder.Services.AddFileStorage(builder.Configuration);
builder.Services.AddDocumentConversion();
builder.Services.AddHostedService<ConversionConsumerWorker>();

await builder.Build().RunAsync();
