using System.Diagnostics;
using System.Globalization;
using DocumentProcessing.Contracts.Messaging;
using DocumentProcessing.Messaging.RabbitMq.Channels;
using DocumentProcessing.Messaging.RabbitMq.Configuration;
using DocumentProcessing.Messaging.RabbitMq.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace DocumentProcessing.Messaging.RabbitMq.Publishing;

internal sealed class RabbitMqPublisher :
    IRabbitMqPublisher
{
    private readonly IRabbitMqChannelFactory _channelFactory;
    private readonly IMessageSerializer _serializer;
    private readonly RabbitMqPublisherOptions _options;
    private readonly ILogger<RabbitMqPublisher> _logger;

    /*
     * RabbitMQ publisher channel'ı eşzamanlı publish için güvenli
     * değildir. Hem channel oluşturmayı hem publish işlemini bu
     * semaphore ile sıralıyoruz.
     */
    private readonly SemaphoreSlim _publishLock = new(1, 1);

    private IChannel? _channel;
    private bool _disposed;

    public RabbitMqPublisher(
        IRabbitMqChannelFactory channelFactory,
        IMessageSerializer serializer,
        IOptions<RabbitMqPublisherOptions> options,
        ILogger<RabbitMqPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(channelFactory);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _channelFactory = channelFactory;
        _serializer = serializer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync<TMessage>(
        MessageEnvelope<TMessage> envelope,
        RabbitMqPublishDestination destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(destination);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            destination.Exchange);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            destination.RoutingKey);

        ThrowIfDisposed();

        /*
         * Serialization channel lock'undan önce yapılıyor.
         *
         * Böylece farklı thread'ler JSON hazırlayabilir; yalnızca
         * RabbitMQ channel kullanımı sıraya alınır.
         */
        ReadOnlyMemory<byte> body =
            _serializer.Serialize(envelope);

        BasicProperties properties =
            CreateBasicProperties(envelope);

        await _publishLock
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            IChannel channel =
                await GetOrCreateChannelAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            using CancellationTokenSource confirmationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            confirmationTokenSource.CancelAfter(
                _options.ConfirmationTimeout);

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                await channel
                    .BasicPublishAsync(
                        exchange: destination.Exchange,
                        routingKey: destination.RoutingKey,
                        mandatory: true,
                        basicProperties: properties,
                        body: body,
                        cancellationToken:
                            confirmationTokenSource.Token)
                    .ConfigureAwait(false);

                stopwatch.Stop();

                _logger.LogInformation(
                    "RabbitMQ message published and confirmed. " +
                    "MessageId: {MessageId}, " +
                    "MessageType: {MessageType}, " +
                    "Exchange: {Exchange}, " +
                    "RoutingKey: {RoutingKey}, " +
                    "BodySize: {BodySize}, " +
                    "DurationMs: {DurationMs}",
                    envelope.MessageId,
                    envelope.MessageType,
                    destination.Exchange,
                    destination.RoutingKey,
                    body.Length,
                    stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (PublishException exception)
            {
                RabbitMqPublishFailureKind failureKind =
                    exception.IsReturn
                        ? RabbitMqPublishFailureKind.Unroutable
                        : RabbitMqPublishFailureKind.BrokerRejected;

                _logger.LogError(
                    exception,
                    "RabbitMQ broker did not accept the message. " +
                    "FailureKind: {FailureKind}, " +
                    "MessageId: {MessageId}, " +
                    "PublishSequenceNumber: {PublishSequenceNumber}, " +
                    "Exchange: {Exchange}, " +
                    "RoutingKey: {RoutingKey}",
                    failureKind,
                    envelope.MessageId,
                    exception.PublishSequenceNumber,
                    destination.Exchange,
                    destination.RoutingKey);

                throw new RabbitMqPublishException(
                    failureKind,
                    envelope.MessageId,
                    destination.Exchange,
                    destination.RoutingKey,
                    outcomeUnknown: false,
                    exception);
            }
            catch (OperationCanceledException exception)
                when (!cancellationToken.IsCancellationRequested)
            {
                /*
                 * Caller iptal etmedi ama linked token iptal oldu.
                 * Yani confirmation timeout gerçekleşti.
                 *
                 * Broker mesajı almış olabilir; sonucu bilmiyoruz.
                 */
                await InvalidateChannelAsync(channel)
                    .ConfigureAwait(false);

                _logger.LogError(
                    exception,
                    "RabbitMQ publisher confirmation timed out. " +
                    "MessageId: {MessageId}, " +
                    "ConfirmationTimeout: {ConfirmationTimeout}",
                    envelope.MessageId,
                    _options.ConfirmationTimeout);

                throw new RabbitMqPublishException(
                    RabbitMqPublishFailureKind
                        .ConfirmationTimedOut,
                    envelope.MessageId,
                    destination.Exchange,
                    destination.RoutingKey,
                    outcomeUnknown: true,
                    exception);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                /*
                 * Publish devam ederken caller cancellation geldi.
                 * Sonuç kesin olmadığı için channel'ı yeniden
                 * kullanmıyoruz.
                 */
                await InvalidateChannelAsync(channel)
                    .ConfigureAwait(false);

                _logger.LogWarning(
                    "RabbitMQ publish was cancelled by the caller. " +
                    "The delivery outcome is unknown. " +
                    "MessageId: {MessageId}",
                    envelope.MessageId);

                throw;
            }
            catch (RabbitMQClientException exception)
            {
                await InvalidateChannelAsync(channel)
                    .ConfigureAwait(false);

                _logger.LogError(
                    exception,
                    "RabbitMQ client failed while publishing. " +
                    "MessageId: {MessageId}, " +
                    "Exchange: {Exchange}, " +
                    "RoutingKey: {RoutingKey}",
                    envelope.MessageId,
                    destination.Exchange,
                    destination.RoutingKey);

                throw new RabbitMqPublishException(
                    RabbitMqPublishFailureKind.TransportFailure,
                    envelope.MessageId,
                    destination.Exchange,
                    destination.RoutingKey,
                    outcomeUnknown: true,
                    exception);
            }
            catch (Exception exception)
            {
                await InvalidateChannelAsync(channel)
                    .ConfigureAwait(false);

                _logger.LogError(
                    exception,
                    "Unexpected RabbitMQ publish failure. " +
                    "MessageId: {MessageId}",
                    envelope.MessageId);

                throw new RabbitMqPublishException(
                    RabbitMqPublishFailureKind.TransportFailure,
                    envelope.MessageId,
                    destination.Exchange,
                    destination.RoutingKey,
                    outcomeUnknown: true,
                    exception);
            }
        }
        finally
        {
            _publishLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _publishLock
            .WaitAsync(CancellationToken.None)
            .ConfigureAwait(false);

        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            IChannel? channel = _channel;
            _channel = null;

            await DisposeChannelAsync(channel)
                .ConfigureAwait(false);
        }
        finally
        {
            _publishLock.Release();
            _publishLock.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private async Task<IChannel> GetOrCreateChannelAsync(
        CancellationToken cancellationToken)
    {
        IChannel? currentChannel = _channel;

        if (currentChannel is { IsOpen: true })
        {
            return currentChannel;
        }

        await DisposeChannelAsync(currentChannel)
            .ConfigureAwait(false);

        IChannel newChannel =
            await _channelFactory
                .CreateChannelAsync(
                    RabbitMqChannelPurpose.Publisher,
                    cancellationToken)
                .ConfigureAwait(false);

        newChannel.BasicReturnAsync +=
            HandleBasicReturnAsync;

        _channel = newChannel;

        return newChannel;
    }

    private BasicProperties CreateBasicProperties<TMessage>(
        MessageEnvelope<TMessage> envelope)
    {
        Dictionary<string, object?> headers =
            new(StringComparer.Ordinal)
            {
                [MessageHeaders.MessageId] =
                    envelope.MessageId.ToString("D"),

                [MessageHeaders.MessageType] =
                    envelope.MessageType,

                [MessageHeaders.MessageVersion] =
                    envelope.MessageVersion,

                [MessageHeaders.Producer] =
                    envelope.Producer,

                [MessageHeaders.Attempt] =
                    envelope.Attempt,

                [MessageHeaders.CreatedAtUtc] =
                    envelope.CreatedAtUtc.ToString(
                        "O",
                        CultureInfo.InvariantCulture)
            };

        if (!string.IsNullOrWhiteSpace(
                envelope.CorrelationId))
        {
            headers[MessageHeaders.CorrelationId] =
                envelope.CorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(
                envelope.CausationId))
        {
            headers[MessageHeaders.CausationId] =
                envelope.CausationId;
        }

        return new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",

            /*
             * Delivery mode 2.
             * Mesajı persistent yapar.
             */
            Persistent = true,

            MessageId =
                envelope.MessageId.ToString("D"),

            CorrelationId =
                envelope.CorrelationId,

            Type =
                envelope.MessageType,

            AppId =
                envelope.Producer,

            Timestamp =
                new AmqpTimestamp(
                    envelope.CreatedAtUtc
                        .ToUnixTimeSeconds()),

            Headers = headers
        };
    }

    private Task HandleBasicReturnAsync(
        object sender,
        BasicReturnEventArgs eventArgs)
    {
        _logger.LogError(
            "RabbitMQ returned an unroutable message. " +
            "ReplyCode: {ReplyCode}, " +
            "ReplyText: {ReplyText}, " +
            "MessageId: {MessageId}, " +
            "Exchange: {Exchange}, " +
            "RoutingKey: {RoutingKey}",
            eventArgs.ReplyCode,
            eventArgs.ReplyText,
            eventArgs.BasicProperties.MessageId,
            eventArgs.Exchange,
            eventArgs.RoutingKey);

        return Task.CompletedTask;
    }

    private async Task InvalidateChannelAsync(
        IChannel channel)
    {
        if (!ReferenceEquals(_channel, channel))
        {
            return;
        }

        _channel = null;

        await DisposeChannelAsync(channel)
            .ConfigureAwait(false);
    }

    private async Task DisposeChannelAsync(
        IChannel? channel)
    {
        if (channel is null)
        {
            return;
        }

        try
        {
            channel.BasicReturnAsync -=
                HandleBasicReturnAsync;

            await channel
                .DisposeAsync()
                .ConfigureAwait(false);

            _logger.LogDebug(
                "RabbitMQ publisher channel disposed.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "An error occurred while disposing the " +
                "RabbitMQ publisher channel.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}