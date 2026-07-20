using DocumentProcessing.Contracts.Messages;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Messaging.RabbitMq.DependencyInjection;
using DocumentProcessing.Messaging.RabbitMq.Services;
using DocumentProcessing.Messaging.RabbitMq.Topology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: publisher <absolute-pdf-path>");
    return 1;
}

string inputPath = Path.GetFullPath(args[0]);
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddRabbitMqMessaging(builder.Configuration);
await using IHost host = builder.Build();

RabbitMqTopologyInitializer topology = host.Services.GetRequiredService<RabbitMqTopologyInitializer>();
IIntegrationEventPublisher publisher = host.Services.GetRequiredService<IIntegrationEventPublisher>();
await topology.InitializeAsync(CancellationToken.None);

Guid jobId = Guid.NewGuid();
ConversionRequested message = new()
{
    JobId = jobId,
    CorrelationId = Guid.NewGuid().ToString("N"),
    SourceReference = $"local://{Uri.EscapeDataString(Path.GetFileName(inputPath))}",
    SourceFileName = Path.GetFileName(inputPath),
    Profile = "display-copy"
};

await publisher.PublishAsync(message, RabbitMqTopology.RequestedRoutingKey, CancellationToken.None);
Console.WriteLine($"Published JobId: {jobId}");
return 0;
