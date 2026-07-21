using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocumentProcessing.Messaging.RabbitMq.Topology;

internal sealed class RabbitMqTopologyHostedService : IHostedService
{
    private readonly IRabbitMqTopologyInitializer _initializer;
    private readonly ILogger<RabbitMqTopologyHostedService> _logger;

    public RabbitMqTopologyHostedService(
        IRabbitMqTopologyInitializer initializer,
        ILogger<RabbitMqTopologyHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(initializer);
        ArgumentNullException.ThrowIfNull(logger);

        _initializer = initializer;
        _logger = logger;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "RabbitMQ topology startup initialization is running.");

        await _initializer
            .InitializeAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}