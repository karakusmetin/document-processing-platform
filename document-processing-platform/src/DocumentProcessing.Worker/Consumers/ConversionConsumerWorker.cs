using DocumentProcessing.Messaging.RabbitMq.Channels;
using DocumentProcessing.Messaging.RabbitMq.Configuration;
using DocumentProcessing.Messaging.RabbitMq.Serialization;
using DocumentProcessing.Worker.Consumers;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DocumentProcessing.Worker;

internal sealed class ConversionConsumerWorker : BackgroundService
{
    private readonly IRabbitMqChannelFactory _channelFactory;
    private readonly IConversionRequestMessageHandler _messageHandler;
    private readonly RabbitMqConsumerOptions _consumerOptions;
    private readonly RabbitMqTopologyOptions _topologyOptions;
    private readonly ILogger<ConversionConsumerWorker> _logger;

    private readonly InFlightMessageTracker _inFlightTracker =
        new();

    private IChannel? _channel;
    private AsyncEventingBasicConsumer? _consumer;
    private CancellationTokenSource? _processingCancellationSource;
    private string? _consumerTag;

    private int _stopping;

    internal ConversionConsumerWorker(
        IRabbitMqChannelFactory channelFactory,
        IConversionRequestMessageHandler messageHandler,
        IOptions<RabbitMqConsumerOptions> consumerOptions,
        IOptions<RabbitMqTopologyOptions> topologyOptions,
        ILogger<ConversionConsumerWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(channelFactory);
        ArgumentNullException.ThrowIfNull(messageHandler);
        ArgumentNullException.ThrowIfNull(consumerOptions);
        ArgumentNullException.ThrowIfNull(topologyOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _channelFactory = channelFactory;
        _messageHandler = messageHandler;
        _consumerOptions = consumerOptions.Value;
        _topologyOptions = topologyOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _processingCancellationSource =
            new CancellationTokenSource();

        try
        {
            await StartConsumerAsync(
                    stoppingToken)
                .ConfigureAwait(false);

            try
            {
                await Task
                    .Delay(
                        Timeout.InfiniteTimeSpan,
                        stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "RabbitMQ conversion consumer stop was requested.");
            }
        }
        finally
        {
            await StopConsumerAsync()
                .ConfigureAwait(false);

            _processingCancellationSource.Dispose();
            _processingCancellationSource = null;
        }
    }

    private async Task StartConsumerAsync(
        CancellationToken cancellationToken)
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
            CreateConsumerTag(
                _consumerOptions.ConsumerTagPrefix);

        string actualConsumerTag =
            await channel
                .BasicConsumeAsync(
                    queue:
                        _topologyOptions
                            .ConversionRequestQueue,
                    autoAck: false,
                    consumerTag:
                        requestedConsumerTag,
                    consumer:
                        consumer,
                    cancellationToken:
                        cancellationToken)
                .ConfigureAwait(false);

        _consumerTag = actualConsumerTag;

        _logger.LogInformation(
            "RabbitMQ conversion consumer started. " +
            "Queue: {Queue}, " +
            "ConsumerTag: {ConsumerTag}, " +
            "PrefetchCount: {PrefetchCount}",
            _topologyOptions.ConversionRequestQueue,
            actualConsumerTag,
            _consumerOptions.PrefetchCount);
    }

    private async Task HandleMessageAsync(
        object sender,
        BasicDeliverEventArgs eventArgs)
    {
        /*
         * Callback henüz başlamışken servis kapanıyor olabilir.
         * Track önce alınır; ardından stopping kontrol edilir.
         */
        using IDisposable trackingLease =
            _inFlightTracker.Track();

        if (Volatile.Read(ref _stopping) == 1)
        {
            _logger.LogDebug(
                "RabbitMQ delivery was ignored because the " +
                "consumer is stopping. DeliveryTag: {DeliveryTag}",
                eventArgs.DeliveryTag);

            /*
             * ACK/NACK gönderilmiyor.
             * Channel kapanınca teslimat tekrar queue'ya döner.
             */
            return;
        }

        IChannel? channel = _channel;

        if (channel is null || !channel.IsOpen)
        {
            _logger.LogWarning(
                "RabbitMQ delivery could not be processed because " +
                "the consumer channel is unavailable. " +
                "DeliveryTag: {DeliveryTag}",
                eventArgs.DeliveryTag);

            return;
        }

        CancellationToken processingToken =
            _processingCancellationSource?.Token ??
            CancellationToken.None;

        /*
         * RabbitMQ.Client tarafından yönetilen body belleğini
         * callback dışındaki katmana doğrudan taşımıyoruz.
         */
        byte[] body =
            eventArgs.Body.ToArray();

        ConversionRequestDelivery delivery =
            new(
                Body:
                    body,

                Redelivered:
                    eventArgs.Redelivered,

                Exchange:
                    eventArgs.Exchange,

                RoutingKey:
                    eventArgs.RoutingKey);

        try
        {
            ConsumerMessageHandlingResult result =
                await _messageHandler
                    .HandleAsync(
                        delivery,
                        processingToken)
                    .ConfigureAwait(false);

            await ApplyDispositionAsync(
                    channel,
                    eventArgs.DeliveryTag,
                    result)
                .ConfigureAwait(false);
        }
        catch (MessageSerializationException exception)
        {
            _logger.LogError(
                exception,
                "RabbitMQ message could not be deserialized. " +
                "The message will be dead-lettered. " +
                "DeliveryTag: {DeliveryTag}, " +
                "Exchange: {Exchange}, " +
                "RoutingKey: {RoutingKey}",
                eventArgs.DeliveryTag,
                eventArgs.Exchange,
                eventArgs.RoutingKey);

            await TryNackAsync(
                    channel,
                    eventArgs.DeliveryTag,
                    requeue: false)
                .ConfigureAwait(false);
        }
        catch (InvalidMessageEnvelopeException exception)
        {
            _logger.LogError(
                exception,
                "RabbitMQ message envelope is invalid. " +
                "The message will be dead-lettered. " +
                "DeliveryTag: {DeliveryTag}",
                eventArgs.DeliveryTag);

            await TryNackAsync(
                    channel,
                    eventArgs.DeliveryTag,
                    requeue: false)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (processingToken.IsCancellationRequested)
        {
            /*
             * Forced shutdown sırasında mesajı ACK/NACK etmiyoruz.
             * Channel kapanınca RabbitMQ teslimatı yeniden queue'ya
             * bırakacak.
             */
            _logger.LogWarning(
                "RabbitMQ message processing was cancelled during " +
                "service shutdown. DeliveryTag: {DeliveryTag}",
                eventArgs.DeliveryTag);
        }
        catch (Exception exception)
        {
            /*
             * Altyapı veya beklenmeyen hata.
             *
             * DPP-001-08 tamamlanana kadar geçici olarak requeue
             * yapılıyor. Sonraki adımda delayed retry kullanılacak.
             */
            _logger.LogError(
                exception,
                "Unexpected error while processing RabbitMQ message. " +
                "The message will be temporarily requeued. " +
                "DeliveryTag: {DeliveryTag}",
                eventArgs.DeliveryTag);

            await TryNackAsync(
                    channel,
                    eventArgs.DeliveryTag,
                    requeue: true)
                .ConfigureAwait(false);
        }
    }

    private async Task ApplyDispositionAsync(
        IChannel channel,
        ulong deliveryTag,
        ConsumerMessageHandlingResult result)
    {
        switch (result.Disposition)
        {
            case ConsumerMessageDisposition.Acknowledge:
                await channel
                    .BasicAckAsync(
                        deliveryTag:
                            deliveryTag,
                        multiple: false,
                        cancellationToken:
                            CancellationToken.None)
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "RabbitMQ message acknowledged. " +
                    "DeliveryTag: {DeliveryTag}, " +
                    "Reason: {Reason}",
                    deliveryTag,
                    result.Reason);

                break;

            case ConsumerMessageDisposition.Requeue:
                await channel
                    .BasicNackAsync(
                        deliveryTag:
                            deliveryTag,
                        multiple: false,
                        requeue: true,
                        cancellationToken:
                            CancellationToken.None)
                    .ConfigureAwait(false);

                _logger.LogWarning(
                    "RabbitMQ message requeued. " +
                    "DeliveryTag: {DeliveryTag}, " +
                    "Reason: {Reason}",
                    deliveryTag,
                    result.Reason);

                break;

            case ConsumerMessageDisposition.DeadLetter:
                await channel
                    .BasicNackAsync(
                        deliveryTag:
                            deliveryTag,
                        multiple: false,
                        requeue: false,
                        cancellationToken:
                            CancellationToken.None)
                    .ConfigureAwait(false);

                _logger.LogWarning(
                    "RabbitMQ message dead-lettered. " +
                    "DeliveryTag: {DeliveryTag}, " +
                    "Reason: {Reason}",
                    deliveryTag,
                    result.Reason);

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(result),
                    result.Disposition,
                    "Unsupported consumer message disposition.");
        }
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
                    multiple: false,
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
                "RabbitMQ message could not be negatively " +
                "acknowledged. DeliveryTag: {DeliveryTag}, " +
                "Requeue: {Requeue}",
                deliveryTag,
                requeue);
        }
    }

    private async Task StopConsumerAsync()
    {
        if (Interlocked.Exchange(
                ref _stopping,
                1) == 1)
        {
            return;
        }

        IChannel? channel = _channel;
        AsyncEventingBasicConsumer? consumer = _consumer;
        string? consumerTag = _consumerTag;

        using CancellationTokenSource shutdownTimeoutSource =
            new(_consumerOptions.ShutdownTimeout);

        CancellationToken shutdownToken =
            shutdownTimeoutSource.Token;

        bool gracefulShutdown = true;

        try
        {
            if (channel is { IsOpen: true } &&
                !string.IsNullOrWhiteSpace(consumerTag))
            {
                _logger.LogInformation(
                    "Cancelling RabbitMQ consumer. " +
                    "ConsumerTag: {ConsumerTag}",
                    consumerTag);

                await channel
                    .BasicCancelAsync(
                        consumerTag:
                            consumerTag,
                        noWait: false,
                        cancellationToken:
                            shutdownToken)
                    .ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Waiting for active RabbitMQ message processing " +
                "to finish. ActiveCount: {ActiveCount}",
                _inFlightTracker.ActiveCount);

            await _inFlightTracker
                .WaitForDrainAsync(
                    shutdownToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "All active RabbitMQ messages were drained.");
        }
        catch (OperationCanceledException)
            when (shutdownToken.IsCancellationRequested)
        {
            gracefulShutdown = false;

            _logger.LogWarning(
                "RabbitMQ consumer shutdown timeout expired. " +
                "Active message processing will be cancelled. " +
                "ShutdownTimeout: {ShutdownTimeout}, " +
                "ActiveCount: {ActiveCount}",
                _consumerOptions.ShutdownTimeout,
                _inFlightTracker.ActiveCount);

            _processingCancellationSource?.Cancel();
        }
        catch (Exception exception)
        {
            gracefulShutdown = false;

            _logger.LogWarning(
                exception,
                "An error occurred while stopping the " +
                "RabbitMQ consumer.");

            _processingCancellationSource?.Cancel();
        }
        finally
        {
            if (consumer is not null)
            {
                consumer.ReceivedAsync -=
                    HandleMessageAsync;
            }

            _consumer = null;
            _consumerTag = null;
            _channel = null;

            await DisposeChannelAsync(
                    channel,
                    gracefulShutdown)
                .ConfigureAwait(false);
        }
    }

    private async Task DisposeChannelAsync(
        IChannel? channel,
        bool gracefulShutdown)
    {
        if (channel is null)
        {
            return;
        }

        try
        {
            if (gracefulShutdown &&
                channel.IsOpen)
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
                "RabbitMQ conversion consumer channel disposed.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "An error occurred while disposing the " +
                "RabbitMQ conversion consumer channel.");
        }
    }

    private static string CreateConsumerTag(
        string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        return
            $"{prefix}.{Guid.NewGuid():N}";
    }
}