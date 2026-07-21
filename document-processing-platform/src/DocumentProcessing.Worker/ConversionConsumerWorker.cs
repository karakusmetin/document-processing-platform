using DocumentProcessing.Contracts.Messaging;
using DocumentProcessing.Contracts.Messages;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Messaging.RabbitMq.Configuration;
using DocumentProcessing.Messaging.RabbitMq.Connection;
using DocumentProcessing.Messaging.RabbitMq.Serialization;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DocumentProcessing.Worker;

public sealed class ConversionConsumerWorker(
    IRabbitMqConnectionProvider connectionProvider,
    IMessageSerializer messageSerializer,
    IConversionOrchestrator orchestrator,
    IMessagePublisher publisher,
    IOptions<RabbitMqConsumerOptions> consumerOptions,
    IOptions<RabbitMqTopologyOptions> topologyOptions,
    ILogger<ConversionConsumerWorker> logger)
    : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        /*
         * Topology burada tekrar initialize edilmiyor.
         *
         * AddRabbitMqTopologyInitialization() ile kaydedilen
         * RabbitMqTopologyHostedService, bu worker başlamadan önce
         * topology initialization işlemini gerçekleştiriyor.
         */

        IConnection connection =
            await connectionProvider
                .GetConnectionAsync(stoppingToken)
                .ConfigureAwait(false);

        _channel =
            await connection
                .CreateChannelAsync(
                    cancellationToken: stoppingToken)
                .ConfigureAwait(false);

        await _channel
            .BasicQosAsync(
                prefetchSize: 0,
                prefetchCount:
                    consumerOptions.Value.PrefetchCount,
                global: false,
                cancellationToken: stoppingToken)
            .ConfigureAwait(false);

        AsyncEventingBasicConsumer consumer =
            new(_channel);

        consumer.ReceivedAsync +=
            HandleMessageAsync;

        string queueName =
            topologyOptions.Value.ConversionRequestQueue;

        await _channel
            .BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Listening on RabbitMQ queue {Queue}. " +
            "PrefetchCount: {PrefetchCount}",
            queueName,
            consumerOptions.Value.PrefetchCount);

        await Task
            .Delay(
                Timeout.InfiniteTimeSpan,
                stoppingToken)
            .ConfigureAwait(false);
    }

    private async Task HandleMessageAsync(
        object sender,
        BasicDeliverEventArgs eventArgs)
    {
        IChannel? channel = _channel;

        if (channel is null || !channel.IsOpen)
        {
            logger.LogWarning(
                "RabbitMQ message was received while the " +
                "consumer channel was unavailable.");

            return;
        }

        try
        {
            /*
             * RabbitMQ body artık çıplak ConversionRequested değil:
             *
             * MessageEnvelope<ConversionRequested>
             *
             * IMessageSerializer kullanarak publisher ile consumer'ın
             * aynı JSON kurallarını kullanmasını sağlıyoruz.
             */
            MessageEnvelope<ConversionRequested> envelope =
                messageSerializer
                    .Deserialize<ConversionRequested>(
                        eventArgs.Body);

            ValidateEnvelope(envelope);

            ConversionRequested message =
                envelope.Payload;

            string correlationId =
                !string.IsNullOrWhiteSpace(
                    envelope.CorrelationId)
                    ? envelope.CorrelationId
                    : message.CorrelationId;

            using IDisposable? scope =
                logger.BeginScope(
                    new Dictionary<string, object>
                    {
                        ["JobId"] = message.JobId,
                        ["MessageId"] =
                            envelope.MessageId,
                        ["CorrelationId"] =
                            correlationId,
                        ["Attempt"] =
                            envelope.Attempt
                    });

            ConversionExecutionResult result =
                await orchestrator
                    .ExecuteAsync(
                        new ConversionRequest
                        {
                            JobId =
                                message.JobId,

                            CorrelationId =
                                correlationId,

                            SourceReference =
                                message.SourceReference,

                            SourceFileName =
                                message.SourceFileName,

                            Profile =
                                message.Profile,

                            /*
                             * Retry/attempt bilgisinin asıl kaynağı
                             * envelope'dur.
                             */
                            Attempt =
                                envelope.Attempt
                        },
                        CancellationToken.None)
                    .ConfigureAwait(false);

            /*
             * ConversionCompleted ve ConversionFailed mesajları,
             * gelen request mesajının sonucudur.
             *
             * Bu nedenle:
             *
             * CorrelationId aynı kalır.
             * CausationId gelen request'in MessageId değeridir.
             * Yeni event ilk kez yayınlandığı için Attempt = 1 olur.
             */
            MessagePublishContext publishContext =
                new()
                {
                    CorrelationId =
                        correlationId,

                    CausationId =
                        envelope.MessageId.ToString("D"),

                    Attempt = 1
                };

            if (result.IsSuccess)
            {
                ConversionCompleted completed =
                    new()
                    {
                        JobId =
                            message.JobId,

                        CorrelationId =
                            correlationId,

                        OutputReference =
                            result.OutputReference!,

                        OutputFormat =
                            "pdf",

                        OutputSize =
                            result.OutputSize,

                        OutputSha256 =
                            result.OutputSha256!,

                        PageCount =
                            result.PageCount,

                        Provider =
                            result.Provider!
                    };

                /*
                 * Artık routing key vermiyoruz.
                 *
                 * RabbitMqMessageRouteResolver,
                 * ConversionCompleted türünü EventExchange ve
                 * ConversionCompletedRoutingKey ile eşleştirecek.
                 */
                await publisher
                    .PublishAsync(
                        completed,
                        publishContext,
                        CancellationToken.None)
                    .ConfigureAwait(false);

                /*
                 * Result eventi publisher confirm aldıktan sonra
                 * request mesajını ACK ediyoruz.
                 *
                 * Böylece completed eventi yayınlanmadan request
                 * queue'dan silinmez.
                 */
                await channel
                    .BasicAckAsync(
                        deliveryTag:
                            eventArgs.DeliveryTag,
                        multiple: false)
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "Conversion completed using provider {Provider}.",
                    result.Provider);

                return;
            }

            ConversionFailed failed =
                new()
                {
                    JobId =
                        message.JobId,

                    CorrelationId =
                        correlationId,

                    ErrorCode =
                        result.ErrorCode!,

                    Message =
                        result.ErrorMessage!,

                    Retryable =
                        result.Retryable,

                    FailedStage =
                        result.FailedStage!,

                    Attempt =
                        envelope.Attempt,

                    DiagnosticId =
                        Guid.NewGuid().ToString("N")
                };

            /*
             * ConversionFailed route'u da mesaj türünden
             * otomatik çözülecek.
             */
            await publisher
                .PublishAsync(
                    failed,
                    publishContext,
                    CancellationToken.None)
                .ConfigureAwait(false);

            /*
             * Bu requeue davranışı şimdilik mevcut akışı koruyor.
             *
             * DPP-001-07/08 adımında retryable mesajı doğrudan
             * requeue:true yapmak yerine 10s/60s/300s retry
             * queue'larına publish edeceğiz.
             */
            await channel
                .BasicNackAsync(
                    deliveryTag:
                        eventArgs.DeliveryTag,
                    multiple: false,
                    requeue:
                        result.Retryable)
                .ConfigureAwait(false);

            logger.LogWarning(
                "Conversion failed. ErrorCode: {ErrorCode}, " +
                "Retryable: {Retryable}",
                result.ErrorCode,
                result.Retryable);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unhandled error while processing RabbitMQ message. " +
                "DeliveryTag: {DeliveryTag}",
                eventArgs.DeliveryTag);

            /*
             * Deserialize edilemeyen veya beklenmeyen şekilde
             * hata veren mesaj ana queue'ya hemen geri bırakılmıyor.
             *
             * Conversion request queue'nun DLX ayarı bulunduğu için
             * requeue:false mesajı dead-letter akışına gönderir.
             */
            await channel
                .BasicNackAsync(
                    deliveryTag:
                        eventArgs.DeliveryTag,
                    multiple: false,
                    requeue: false)
                .ConfigureAwait(false);
        }
    }

    private static void ValidateEnvelope(
        MessageEnvelope<ConversionRequested> envelope)
    {
        if (!string.Equals(
                envelope.MessageType,
                ConversionMessageTypes.ConversionRequested,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unexpected RabbitMQ message type. " +
                $"Expected: " +
                $"'{ConversionMessageTypes.ConversionRequested}', " +
                $"Actual: '{envelope.MessageType}'.");
        }

        if (!string.Equals(
                envelope.MessageVersion,
                ConversionMessageVersions.V1,
                StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Unsupported ConversionRequested message version. " +
                $"Expected: '{ConversionMessageVersions.V1}', " +
                $"Actual: '{envelope.MessageVersion}'.");
        }

        if (envelope.Attempt < 1)
        {
            throw new InvalidOperationException(
                $"Message attempt must be greater than zero. " +
                $"Actual: {envelope.Attempt}.");
        }
    }

    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        IChannel? channel = _channel;
        _channel = null;

        if (channel is not null)
        {
            try
            {
                await channel
                    .DisposeAsync()
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "RabbitMQ conversion consumer channel disposed.");
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "An error occurred while disposing the " +
                    "RabbitMQ conversion consumer channel.");
            }
        }

        await base
            .StopAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}