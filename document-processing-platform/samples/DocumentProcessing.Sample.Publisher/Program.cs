using DocumentProcessing.Contracts.Messages;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Messaging.RabbitMq.DependencyInjection;
using DocumentProcessing.Messaging.RabbitMq.Topology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

if (args.Length != 1)
{
    Console.Error.WriteLine(
        "Usage: publisher <absolute-pdf-path>");

    return 1;
}

string inputPath = Path.GetFullPath(args[0]);

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine(
        $"Input file was not found: {inputPath}");

    return 1;
}

HostApplicationBuilder builder =
    Host.CreateApplicationBuilder(args);

builder.Services.AddRabbitMqMessaging(builder.Configuration).AddRabbitMqTopologyInitialization();

using IHost host = builder.Build();

await host.StartAsync();

try
{
    IIntegrationEventPublisher publisher =host.Services.GetRequiredService<IIntegrationEventPublisher>();

    Guid jobId = Guid.NewGuid();

    ConversionRequested message = new()
    {
        JobId = jobId,
        CorrelationId = Guid.NewGuid().ToString("N"),
        SourceReference =
            $"local://{Uri.EscapeDataString(
                Path.GetFileName(inputPath))}",
        SourceFileName = Path.GetFileName(inputPath),
        Profile = "display-copy"
    };

    await publisher.PublishAsync(
        message,
        RabbitMqTopology.RequestedRoutingKey,
        CancellationToken.None);

    Console.WriteLine(
        $"Published JobId: {jobId}");

    return 0;
}
finally
{
    await host.StopAsync();
}