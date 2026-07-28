using Queue.Messaging.Abstractions;
using DocumentProcessing.Contracts.Messages;
using Queue.Messaging.RabbitMq.DependencyInjection;
using Microsoft.Extensions.Configuration;
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


IConfigurationSection topologySection =
    builder.Configuration.GetSection(
        "RabbitMq:Topology");

string commandExchange =
    topologySection["CommandExchange"]
    ?? throw new InvalidOperationException(
        "RabbitMQ command exchange is not configured.");

string requestedRoutingKey =
    topologySection["ConversionRequestedRoutingKey"]
    ?? throw new InvalidOperationException(
        "RabbitMQ requested routing key is not configured.");

builder.Services
    .AddRabbitMqMessaging(builder.Configuration);

builder.Services.AddRabbitMqMessageRoute<
    ConversionRequested>(
    route =>
    {
        route.Exchange =
            commandExchange;

        route.RoutingKey =
            requestedRoutingKey;

        route.MessageType =
            ConversionMessageTypes.ConversionRequested;

        route.MessageVersion =
            ConversionMessageVersions.V1;
    });

using IHost host =
    builder.Build();

await host.StartAsync();

try
{
    IMessagePublisher publisher =
        host.Services.GetRequiredService<
            IMessagePublisher>();

    Guid jobId =
        Guid.NewGuid();

    string correlationId =
        Guid.NewGuid().ToString("N");

    ConversionRequested message =
        new()
        {
            JobId =
                jobId,

            CorrelationId =
                correlationId,

            SourceReference =
                $"local://{Uri.EscapeDataString(
                    Path.GetFileName(inputPath))}",

            SourceFileName =
                Path.GetFileName(inputPath),

            Profile =
                "display-copy"
        };

    MessagePublishContext publishContext =
        new()
        {
            CorrelationId =
                correlationId,

            Attempt = 1
        };

    await publisher
        .PublishAsync(
            message,
            publishContext,
            CancellationToken.None)
        .ConfigureAwait(false);

    Console.WriteLine(
        $"Published JobId: {jobId}");

    return 0;
}
finally
{
    await host
        .StopAsync()
        .ConfigureAwait(false);
}