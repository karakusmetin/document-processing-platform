using System.Text.Json;
using DocumentProcessing.Contracts.Messages;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Messaging.RabbitMq.Options;
using DocumentProcessing.Messaging.RabbitMq.Services;
using DocumentProcessing.Messaging.RabbitMq.Topology;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DocumentProcessing.Worker;

public sealed class ConversionConsumerWorker(
    RabbitMqConnectionProvider connectionProvider,
    RabbitMqTopologyInitializer topologyInitializer,
    IConversionOrchestrator orchestrator,
    IIntegrationEventPublisher publisher,
    IOptions<RabbitMqOptions> options,
    ILogger<ConversionConsumerWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await topologyInitializer.InitializeAsync(stoppingToken);
        IConnection connection = await connectionProvider.GetConnectionAsync(stoppingToken);
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await _channel.BasicQosAsync(0, options.Value.PrefetchCount, global: false, cancellationToken: stoppingToken);

        AsyncEventingBasicConsumer consumer = new(_channel);
        consumer.ReceivedAsync += HandleMessageAsync;

        await _channel.BasicConsumeAsync(
            RabbitMqTopology.RequestQueue,
            autoAck: false,
            consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation("Listening on queue {Queue}", RabbitMqTopology.RequestQueue);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(object sender, BasicDeliverEventArgs eventArgs)
    {
        if (_channel is null)
        {
            return;
        }

        try
        {
            ConversionRequested? message = JsonSerializer.Deserialize<ConversionRequested>(eventArgs.Body.Span, SerializerOptions);
            if (message is null)
            {
                throw new JsonException("ConversionRequested payload is null.");
            }

            using IDisposable? scope = logger.BeginScope(new Dictionary<string, object>
            {
                ["JobId"] = message.JobId,
                ["CorrelationId"] = message.CorrelationId
            });

            ConversionExecutionResult result = await orchestrator.ExecuteAsync(new ConversionRequest
            {
                JobId = message.JobId,
                CorrelationId = message.CorrelationId,
                SourceReference = message.SourceReference,
                SourceFileName = message.SourceFileName,
                Profile = message.Profile,
                Attempt = message.Attempt
            }, CancellationToken.None);

            if (result.IsSuccess)
            {
                ConversionCompleted completed = new()
                {
                    JobId = message.JobId,
                    CorrelationId = message.CorrelationId,
                    OutputReference = result.OutputReference!,
                    OutputFormat = "pdf",
                    OutputSize = result.OutputSize,
                    OutputSha256 = result.OutputSha256!,
                    PageCount = result.PageCount,
                    Provider = result.Provider!
                };

                await publisher.PublishAsync(completed, RabbitMqTopology.CompletedRoutingKey, CancellationToken.None);
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                logger.LogInformation("Conversion completed using {Provider}", result.Provider);
                return;
            }

            ConversionFailed failed = new()
            {
                JobId = message.JobId,
                CorrelationId = message.CorrelationId,
                ErrorCode = result.ErrorCode!,
                Message = result.ErrorMessage!,
                Retryable = result.Retryable,
                FailedStage = result.FailedStage!,
                Attempt = message.Attempt,
                DiagnosticId = Guid.NewGuid().ToString("N")
            };

            await publisher.PublishAsync(failed, RabbitMqTopology.FailedRoutingKey, CancellationToken.None);
            await _channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: result.Retryable);
            logger.LogWarning("Conversion failed: {ErrorCode}", result.ErrorCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error while processing message");
            await _channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}
