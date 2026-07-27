using Queue.Messaging.Abstractions;
using Queue.Messaging.RabbitMq.Channels;
using Queue.Messaging.RabbitMq.Configuration;
using Queue.Messaging.RabbitMq.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Queue.Messaging.RabbitMq.Consuming;

internal sealed class RabbitMqConsumerInstance<TMessage> :
    IAsyncDisposable
{
    private readonly int _instanceNumber;

    private readonly IRabbitMqChannelFactory _channelFactory;
    private readonly IMessageSerializer _messageSerializer;
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly RabbitMqConsumerOptions _consumerOptions;
    private readonly RabbitMqConsumerDefinition<TMessage> _definition;

    private readonly ILogger _logger;

    private readonly RabbitMqInFlightMessageTracker _inFlightTracker =
        new();

    private readonly CancellationTokenSource _processingCancellationSource =
        new();

    private IChannel? _channel;
    private AsyncEventingBasicConsumer? _consumer;
    private string? _consumerTag;

    private int _started;
    private int _stopping;
    private int _disposed;

    public RabbitMqConsumerInstance(
        int instanceNumber,
        IRabbitMqChannelFactory channelFactory,
        IMessageSerializer messageSerializer,
        IServiceScopeFactory scopeFactory,
        RabbitMqConsumerOptions consumerOptions,
        RabbitMqConsumerDefinition<TMessage> definition,
        ILogger logger)
    {
        if (instanceNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(instanceNumber),
                instanceNumber,
                "Consumer instance number must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(channelFactory);
        ArgumentNullException.ThrowIfNull(messageSerializer);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(consumerOptions);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(logger);

        _instanceNumber = instanceNumber;

        _channelFactory = channelFactory;
        _messageSerializer = messageSerializer;
        _scopeFactory = scopeFactory;

        _consumerOptions = consumerOptions;
        _definition = definition;

        _logger = logger;
    }

    public int InstanceNumber =>
        _instanceNumber;

    public string? ConsumerTag =>
        _consumerTag;

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (Interlocked.Exchange(
                ref _started,
                1) == 1)
        {
            throw new InvalidOperationException(
                $"RabbitMQ consumer instance " +
                $"'{_instanceNumber}' has already been started.");
        }

        try
        {
            IChannel channel =
                await _channelFactory
                    .CreateChannelAsync(
                        RabbitMqChannelPurpose.Consumer,
                        cancellationToken)
                    .ConfigureAwait(false);

            _channel = channel;

            await channel
                .BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount:
                        _consumerOptions.PrefetchCount,
                    global: false,
                    cancellationToken:
                        cancellationToken)
                .ConfigureAwait(false);

            AsyncEventingBasicConsumer consumer =
                new(channel);

            consumer.ReceivedAsync +=
                HandleMessageAsync;

            _consumer = consumer;

            string requestedConsumerTag =
                CreateConsumerTag();

            string actualConsumerTag =
                await channel
                    .BasicConsumeAsync(
                        queue:
                            _definition.QueueName,

                        autoAck:
                            false,

                        consumerTag:
                            requestedConsumerTag,

                        noLocal:
                            false,

                        exclusive:
                            false,

                        arguments:
                            null,

                        consumer:
                            consumer,

                        cancellationToken:
                            cancellationToken)
                    .ConfigureAwait(false);

            _consumerTag = actualConsumerTag;

            _logger.LogInformation(
                "RabbitMQ consumer instance started. " +
                "MessageClrType: {MessageClrType}, " +
                "Queue: {Queue}, " +
                "InstanceNumber: {InstanceNumber}, " +
                "ConsumerTag: {ConsumerTag}, " +
                "PrefetchCount: {PrefetchCount}",
                typeof(TMessage).FullName,
                _definition.QueueName,
                _instanceNumber,
                actualConsumerTag,
                _consumerOptions.PrefetchCount);
        }
        catch
        {
            await DisposeConsumerResourcesAsync()
                .ConfigureAwait(false);

            throw;
        }
    }

    private async Task HandleMessageAsync(
        object sender,
        BasicDeliverEventArgs eventArgs)
    {
        using IDisposable trackingLease =
            _inFlightTracker.Track();

        if (Volatile.Read(ref _stopping) == 1)
        {
            _logger.LogDebug(
                "RabbitMQ delivery was ignored because the " +
                "consumer instance is stopping. " +
                "InstanceNumber: {InstanceNumber}, " +
                "DeliveryTag: {DeliveryTag}",
                _instanceNumber,
                eventArgs.DeliveryTag);

            /*
             * ACK veya NACK göndermiyoruz.
             * Channel kapandığında unacked teslimat yeniden
             * queue'ya alınır.
             */
            return;
        }

        IChannel? channel = _channel;

        if (channel is null || !channel.IsOpen)
        {
            _logger.LogWarning(
                "RabbitMQ delivery cannot be processed because " +
                "the consumer channel is unavailable. " +
                "InstanceNumber: {InstanceNumber}, " +
                "DeliveryTag: {DeliveryTag}",
                _instanceNumber,
                eventArgs.DeliveryTag);

            return;
        }

        /*
         * RabbitMQ.Client tarafından yönetilen body belleğinin
         * yaşam süresini runtime dışına taşımıyoruz.
         */
        byte[] body =
            eventArgs.Body.ToArray();

        try
        {
            MessageEnvelope<TMessage> envelope =
                _messageSerializer
                    .Deserialize<TMessage>(body);

            ValidateEnvelope(envelope);

            RabbitMqDeliveryContext deliveryContext =
                CreateDeliveryContext(eventArgs);

            RabbitMqMessageHandlingResult result =
                await InvokeHandlerAsync(
                        envelope,
                        deliveryContext,
                        _processingCancellationSource.Token)
                    .ConfigureAwait(false);

            await ApplyHandlingResultAsync(
                    channel,
                    eventArgs,
                    body,
                    result)
                .ConfigureAwait(false);
        }
        catch (MessageSerializationException exception)
        {
            await DeadLetterAsync(
                    channel,
                    eventArgs,
                    body,
                    RabbitMqConsumerFailureCodes.MalformedMessage,
                    "RabbitMQ message body could not be deserialized.",
                    Guid.NewGuid().ToString("N"),
                    exception)
                .ConfigureAwait(false);
        }
        catch (RabbitMqMessageContractException exception)
        {
            await DeadLetterAsync(
                    channel,
                    eventArgs,
                    body,
                    exception.FailureCode,
                    exception.Message,
                    Guid.NewGuid().ToString("N"),
                    exception)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (_processingCancellationSource
                .IsCancellationRequested)
        {
            /*
             * Shutdown sırasında mesajı sonuçlandırmıyoruz.
             * Channel kapanınca unacked teslimat tekrar queue'ya
             * döner.
             */
            _logger.LogWarning(
                "RabbitMQ message processing was cancelled during " +
                "consumer shutdown. " +
                "InstanceNumber: {InstanceNumber}, " +
                "DeliveryTag: {DeliveryTag}",
                _instanceNumber,
                eventArgs.DeliveryTag);
        }
        catch (Exception exception)
        {
            /*
             * Handler'ın beklenen business hatalarını kendi
             * retry/dead-letter sonucuna çevirmesi gerekir.
             *
             * Buraya düşen hata beklenmeyen altyapı veya handler
             * hatasıdır. Veri kaybetmemek için geçici olarak
             * requeue ediyoruz.
             */
            _logger.LogError(
                exception,
                "Unhandled exception occurred in RabbitMQ message " +
                "processing. The delivery will be requeued. " +
                "MessageClrType: {MessageClrType}, " +
                "InstanceNumber: {InstanceNumber}, " +
                "DeliveryTag: {DeliveryTag}",
                typeof(TMessage).FullName,
                _instanceNumber,
                eventArgs.DeliveryTag);

            await TryNackAsync(
                    channel,
                    eventArgs.DeliveryTag,
                    requeue: true)
                .ConfigureAwait(false);
        }
    }

    private async Task<RabbitMqMessageHandlingResult>
        InvokeHandlerAsync(
            MessageEnvelope<TMessage> envelope,
            RabbitMqDeliveryContext deliveryContext,
            CancellationToken cancellationToken)
    {
        /*
         * Her mesaj için yeni DI scope.
         *
         * Böylece handler veya alt bağımlılıkları scoped olabilir:
         * DbContext, unit of work, request context vb.
         */
        await using AsyncServiceScope scope =
            _scopeFactory.CreateAsyncScope();

        IRabbitMqMessageHandler<TMessage> handler =
            scope.ServiceProvider
                .GetRequiredService<
                    IRabbitMqMessageHandler<TMessage>>();

        RabbitMqMessageHandlingResult? result =
            await handler
                .HandleAsync(
                    envelope,
                    deliveryContext,
                    cancellationToken)
                .ConfigureAwait(false);

        return result
            ?? throw new InvalidOperationException(
                $"RabbitMQ message handler " +
                $"'{handler.GetType().FullName}' returned null.");
    }

    private async Task ApplyHandlingResultAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        ReadOnlyMemory<byte> body,
        RabbitMqMessageHandlingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        switch (result.Disposition)
        {
            case RabbitMqMessageDisposition.Acknowledge:
                await channel
                    .BasicAckAsync(
                        deliveryTag:
                            eventArgs.DeliveryTag,

                        multiple:
                            false,

                        cancellationToken:
                            CancellationToken.None)
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "RabbitMQ message acknowledged. " +
                    "MessageClrType: {MessageClrType}, " +
                    "InstanceNumber: {InstanceNumber}, " +
                    "DeliveryTag: {DeliveryTag}, " +
                    "Reason: {Reason}",
                    typeof(TMessage).FullName,
                    _instanceNumber,
                    eventArgs.DeliveryTag,
                    result.Reason);

                break;

            case RabbitMqMessageDisposition.DeadLetter:
                await DeadLetterAsync(
                        channel,
                        eventArgs,
                        body,
                        result.FailureCode ??
                        RabbitMqConsumerFailureCodes.HandlerRejected,
                        result.Reason,
                        result.DiagnosticId ??
                        Guid.NewGuid().ToString("N"))
                    .ConfigureAwait(false);

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(result),
                    result.Disposition,
                    "Unsupported RabbitMQ message disposition.");
        }
    }

    private async Task DeadLetterAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        ReadOnlyMemory<byte> body,
        string failureCode,
        string reason,
        string diagnosticId,
        Exception? exception = null)
    {
        string bodySha256 =
            RabbitMqMessageBodyFingerprint
                .ComputeSha256(body.Span);

        if (exception is null)
        {
            _logger.LogError(
                "RabbitMQ message will be dead-lettered. " +
                "MessageClrType: {MessageClrType}, " +
                "InstanceNumber: {InstanceNumber}, " +
                "FailureCode: {FailureCode}, " +
                "Reason: {Reason}, " +
                "DiagnosticId: {DiagnosticId}, " +
                "BodySha256: {BodySha256}, " +
                "BrokerMessageId: {BrokerMessageId}, " +
                "BrokerCorrelationId: {BrokerCorrelationId}, " +
                "BrokerMessageType: {BrokerMessageType}, " +
                "Exchange: {Exchange}, " +
                "RoutingKey: {RoutingKey}, " +
                "Redelivered: {Redelivered}, " +
                "DeliveryTag: {DeliveryTag}",
                typeof(TMessage).FullName,
                _instanceNumber,
                failureCode,
                reason,
                diagnosticId,
                bodySha256,
                eventArgs.BasicProperties.MessageId,
                eventArgs.BasicProperties.CorrelationId,
                eventArgs.BasicProperties.Type,
                eventArgs.Exchange,
                eventArgs.RoutingKey,
                eventArgs.Redelivered,
                eventArgs.DeliveryTag);
        }
        else
        {
            _logger.LogError(
                exception,
                "RabbitMQ message will be dead-lettered. " +
                "MessageClrType: {MessageClrType}, " +
                "InstanceNumber: {InstanceNumber}, " +
                "FailureCode: {FailureCode}, " +
                "Reason: {Reason}, " +
                "DiagnosticId: {DiagnosticId}, " +
                "BodySha256: {BodySha256}, " +
                "BrokerMessageId: {BrokerMessageId}, " +
                "BrokerCorrelationId: {BrokerCorrelationId}, " +
                "BrokerMessageType: {BrokerMessageType}, " +
                "Exchange: {Exchange}, " +
                "RoutingKey: {RoutingKey}, " +
                "Redelivered: {Redelivered}, " +
                "DeliveryTag: {DeliveryTag}",
                typeof(TMessage).FullName,
                _instanceNumber,
                failureCode,
                reason,
                diagnosticId,
                bodySha256,
                eventArgs.BasicProperties.MessageId,
                eventArgs.BasicProperties.CorrelationId,
                eventArgs.BasicProperties.Type,
                eventArgs.Exchange,
                eventArgs.RoutingKey,
                eventArgs.Redelivered,
                eventArgs.DeliveryTag);
        }

        await TryNackAsync(
                channel,
                eventArgs.DeliveryTag,
                requeue: false)
            .ConfigureAwait(false);
    }

    private async Task TryNackAsync(
        IChannel channel,
        ulong deliveryTag,
        bool requeue)
    {
        try
        {
            if (!channel.IsOpen)
            {
                return;
            }

            await channel
                .BasicNackAsync(
                    deliveryTag:
                        deliveryTag,

                    multiple:
                        false,

                    requeue:
                        requeue,

                    cancellationToken:
                        CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "RabbitMQ delivery could not be negatively " +
                "acknowledged. " +
                "InstanceNumber: {InstanceNumber}, " +
                "DeliveryTag: {DeliveryTag}, " +
                "Requeue: {Requeue}",
                _instanceNumber,
                deliveryTag,
                requeue);
        }
    }

    private void ValidateEnvelope(
        MessageEnvelope<TMessage> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.MessageId == Guid.Empty)
        {
            throw new RabbitMqMessageContractException(
                RabbitMqConsumerFailureCodes.InvalidEnvelope,
                "RabbitMQ envelope MessageId cannot be empty.");
        }

        if (!string.Equals(
                envelope.MessageType,
                _definition.MessageType,
                StringComparison.Ordinal))
        {
            throw new RabbitMqMessageContractException(
                RabbitMqConsumerFailureCodes.UnsupportedMessageType,
                $"Unexpected RabbitMQ message type. " +
                $"Expected: '{_definition.MessageType}', " +
                $"Actual: '{envelope.MessageType}'.");
        }

        if (!string.Equals(
                envelope.MessageVersion,
                _definition.MessageVersion,
                StringComparison.Ordinal))
        {
            throw new RabbitMqMessageContractException(
                RabbitMqConsumerFailureCodes
                    .UnsupportedMessageVersion,
                $"Unsupported RabbitMQ message version. " +
                $"Expected: '{_definition.MessageVersion}', " +
                $"Actual: '{envelope.MessageVersion}'.");
        }

        if (envelope.Attempt < 1)
        {
            throw new RabbitMqMessageContractException(
                RabbitMqConsumerFailureCodes.InvalidEnvelope,
                $"RabbitMQ message attempt must be greater than zero. " +
                $"Actual: {envelope.Attempt}.");
        }

        if (envelope.Payload is null)
        {
            throw new RabbitMqMessageContractException(
                RabbitMqConsumerFailureCodes.InvalidEnvelope,
                "RabbitMQ envelope payload cannot be null.");
        }
    }

    private static RabbitMqDeliveryContext
        CreateDeliveryContext(
            BasicDeliverEventArgs eventArgs)
    {
        return new RabbitMqDeliveryContext
        {
            Redelivered =
                eventArgs.Redelivered,

            Exchange =
                eventArgs.Exchange,

            RoutingKey =
                eventArgs.RoutingKey,

            BrokerMessageId =
                eventArgs.BasicProperties.MessageId,

            BrokerCorrelationId =
                eventArgs.BasicProperties.CorrelationId,

            BrokerMessageType =
                eventArgs.BasicProperties.Type
        };
    }

    public async Task StopAsync(
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(
                ref _stopping,
                1) == 1)
        {
            return;
        }

        IChannel? channel = _channel;
        string? consumerTag = _consumerTag;

        using CancellationTokenSource configuredTimeoutSource =
            new(_consumerOptions.ShutdownTimeout);

        using CancellationTokenSource linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                configuredTimeoutSource.Token);

        CancellationToken shutdownToken =
            linkedSource.Token;

        try
        {
            if (channel is { IsOpen: true } &&
                !string.IsNullOrWhiteSpace(consumerTag))
            {
                _logger.LogInformation(
                    "Cancelling RabbitMQ consumer instance. " +
                    "InstanceNumber: {InstanceNumber}, " +
                    "ConsumerTag: {ConsumerTag}",
                    _instanceNumber,
                    consumerTag);

                await channel
                    .BasicCancelAsync(
                        consumerTag:
                            consumerTag,

                        noWait:
                            false,

                        cancellationToken:
                            shutdownToken)
                    .ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Waiting for RabbitMQ consumer instance to drain. " +
                "InstanceNumber: {InstanceNumber}, " +
                "ActiveCount: {ActiveCount}",
                _instanceNumber,
                _inFlightTracker.ActiveCount);

            await _inFlightTracker
                .WaitForDrainAsync(shutdownToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "RabbitMQ consumer instance drained successfully. " +
                "InstanceNumber: {InstanceNumber}",
                _instanceNumber);
        }
        catch (OperationCanceledException)
            when (shutdownToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "RabbitMQ consumer instance shutdown timed out. " +
                "Active message processing will be cancelled. " +
                "InstanceNumber: {InstanceNumber}, " +
                "ShutdownTimeout: {ShutdownTimeout}, " +
                "ActiveCount: {ActiveCount}",
                _instanceNumber,
                _consumerOptions.ShutdownTimeout,
                _inFlightTracker.ActiveCount);

            _processingCancellationSource.Cancel();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "An error occurred while stopping RabbitMQ " +
                "consumer instance {InstanceNumber}.",
                _instanceNumber);

            _processingCancellationSource.Cancel();
        }
        finally
        {
            await DisposeConsumerResourcesAsync()
                .ConfigureAwait(false);
        }
    }

    private async Task DisposeConsumerResourcesAsync()
    {
        AsyncEventingBasicConsumer? consumer =
            _consumer;

        IChannel? channel =
            _channel;

        if (consumer is not null)
        {
            consumer.ReceivedAsync -=
                HandleMessageAsync;
        }

        _consumer = null;
        _consumerTag = null;
        _channel = null;

        if (channel is null)
        {
            return;
        }

        try
        {
            if (channel.IsOpen)
            {
                await channel
                    .CloseAsync(
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }

            await channel
                .DisposeAsync()
                .ConfigureAwait(false);

            _logger.LogInformation(
                "RabbitMQ consumer channel disposed. " +
                "InstanceNumber: {InstanceNumber}",
                _instanceNumber);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "An error occurred while disposing RabbitMQ " +
                "consumer channel. " +
                "InstanceNumber: {InstanceNumber}",
                _instanceNumber);
        }
    }

    private string CreateConsumerTag()
    {
        return
            $"{_definition.ConsumerTagPrefix}" +
            $".{Environment.ProcessId}" +
            $".{_instanceNumber}" +
            $".{Guid.NewGuid():N}";
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _disposed,
                1) == 1)
        {
            return;
        }

        await StopAsync(CancellationToken.None)
            .ConfigureAwait(false);

        _processingCancellationSource.Dispose();

        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) == 1,
            this);
    }
}