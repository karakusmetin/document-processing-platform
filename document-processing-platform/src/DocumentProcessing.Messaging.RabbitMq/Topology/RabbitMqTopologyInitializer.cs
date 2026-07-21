using DocumentProcessing.Messaging.RabbitMq.Channels;
using DocumentProcessing.Messaging.RabbitMq.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DocumentProcessing.Messaging.RabbitMq.Topology;

internal sealed class RabbitMqTopologyInitializer :
    IRabbitMqTopologyInitializer
{
    private const bool Durable = true;
    private const bool Exclusive = false;
    private const bool AutoDelete = false;

    private readonly IRabbitMqChannelFactory _channelFactory;
    private readonly RabbitMqTopologyOptions _topologyOptions;
    private readonly RabbitMqRetryOptions _retryOptions;
    private readonly ILogger<RabbitMqTopologyInitializer> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    private bool _initialized;

    public RabbitMqTopologyInitializer(
        IRabbitMqChannelFactory channelFactory,
        IOptions<RabbitMqTopologyOptions> topologyOptions,
        IOptions<RabbitMqRetryOptions> retryOptions,
        ILogger<RabbitMqTopologyInitializer> logger)
    {
        ArgumentNullException.ThrowIfNull(channelFactory);
        ArgumentNullException.ThrowIfNull(topologyOptions);
        ArgumentNullException.ThrowIfNull(retryOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _channelFactory = channelFactory;
        _topologyOptions = topologyOptions.Value;
        _retryOptions = retryOptions.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            _logger.LogDebug(
                "RabbitMQ topology has already been initialized.");

            return;
        }

        await _initializationLock
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            if (_initialized)
            {
                return;
            }

            _logger.LogInformation(
                "RabbitMQ topology initialization started. " +
                "QueueType: {QueueType}",
                _topologyOptions.QueueType);

            await using IChannel channel =
                await _channelFactory
                    .CreateChannelAsync(
                        RabbitMqChannelPurpose.Topology,
                        cancellationToken)
                    .ConfigureAwait(false);

            await DeclareExchangesAsync(
                    channel,
                    cancellationToken)
                .ConfigureAwait(false);

            await DeclareDeadLetterQueueAsync(
                    channel,
                    cancellationToken)
                .ConfigureAwait(false);

            await DeclareConversionRequestQueueAsync(
                    channel,
                    cancellationToken)
                .ConfigureAwait(false);

            await DeclareRetryQueuesAsync(
                    channel,
                    cancellationToken)
                .ConfigureAwait(false);

            _initialized = true;

            _logger.LogInformation(
                "RabbitMQ topology initialization completed.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "RabbitMQ topology initialization was cancelled.");

            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "RabbitMQ topology initialization failed. " +
                "The application will not continue with an " +
                "incomplete or incompatible topology.");

            throw;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task DeclareExchangesAsync(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        await DeclareExchangeAsync(
                channel,
                _topologyOptions.CommandExchange,
                ExchangeType.Direct,
                cancellationToken)
            .ConfigureAwait(false);

        await DeclareExchangeAsync(
                channel,
                _topologyOptions.EventExchange,
                ExchangeType.Topic,
                cancellationToken)
            .ConfigureAwait(false);

        await DeclareExchangeAsync(
                channel,
                _topologyOptions.RetryExchange,
                ExchangeType.Direct,
                cancellationToken)
            .ConfigureAwait(false);

        await DeclareExchangeAsync(
                channel,
                _topologyOptions.DeadLetterExchange,
                ExchangeType.Direct,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task DeclareExchangeAsync(
        IChannel channel,
        string exchangeName,
        string exchangeType,
        CancellationToken cancellationToken)
    {
        await channel
            .ExchangeDeclareAsync(
                exchange: exchangeName,
                type: exchangeType,
                durable: Durable,
                autoDelete: AutoDelete,
                arguments: null,
                passive: false,
                noWait: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug(
            "RabbitMQ exchange declared. " +
            "Exchange: {Exchange}, Type: {ExchangeType}",
            exchangeName,
            exchangeType);
    }

    private async Task DeclareDeadLetterQueueAsync(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> arguments =
            CreateQueueTypeArguments();

        await DeclareQueueAsync(
                channel,
                _topologyOptions.ConversionDeadLetterQueue,
                NullWhenEmpty(arguments),
                cancellationToken)
            .ConfigureAwait(false);

        await channel
            .QueueBindAsync(
                queue:
                    _topologyOptions.ConversionDeadLetterQueue,
                exchange:
                    _topologyOptions.DeadLetterExchange,
                routingKey:
                    _topologyOptions
                        .ConversionDeadLetterRoutingKey,
                arguments: null,
                noWait: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug(
            "RabbitMQ dead-letter queue declared and bound. " +
            "Queue: {Queue}, Exchange: {Exchange}, " +
            "RoutingKey: {RoutingKey}",
            _topologyOptions.ConversionDeadLetterQueue,
            _topologyOptions.DeadLetterExchange,
            _topologyOptions.ConversionDeadLetterRoutingKey);
    }

    private async Task DeclareConversionRequestQueueAsync(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> arguments =
            CreateQueueTypeArguments();

        arguments["x-dead-letter-exchange"] =
            _topologyOptions.DeadLetterExchange;

        arguments["x-dead-letter-routing-key"] =
            _topologyOptions.ConversionDeadLetterRoutingKey;

        await DeclareQueueAsync(
                channel,
                _topologyOptions.ConversionRequestQueue,
                arguments,
                cancellationToken)
            .ConfigureAwait(false);

        await channel
            .QueueBindAsync(
                queue:
                    _topologyOptions.ConversionRequestQueue,
                exchange:
                    _topologyOptions.CommandExchange,
                routingKey:
                    _topologyOptions
                        .ConversionRequestedRoutingKey,
                arguments: null,
                noWait: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug(
            "RabbitMQ conversion request queue declared and bound. " +
            "Queue: {Queue}, Exchange: {Exchange}, " +
            "RoutingKey: {RoutingKey}",
            _topologyOptions.ConversionRequestQueue,
            _topologyOptions.CommandExchange,
            _topologyOptions.ConversionRequestedRoutingKey);
    }

    private async Task DeclareRetryQueuesAsync(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        foreach (int delaySeconds in
                 _retryOptions.DelaySeconds.Order())
        {
            await DeclareRetryQueueAsync(
                    channel,
                    delaySeconds,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task DeclareRetryQueueAsync(
        IChannel channel,
        int delaySeconds,
        CancellationToken cancellationToken)
    {
        string queueName =
            RabbitMqTopologyNameBuilder.GetRetryQueueName(
                _topologyOptions.RetryQueuePrefix,
                delaySeconds);

        string routingKey =
            RabbitMqTopologyNameBuilder.GetRetryRoutingKey(
                _topologyOptions.RetryRoutingKeyPrefix,
                delaySeconds);

        long delayMilliseconds =
            checked((long)delaySeconds * 1000L);

        Dictionary<string, object?> arguments =
            CreateQueueTypeArguments();

        arguments["x-message-ttl"] = delayMilliseconds;

        arguments["x-dead-letter-exchange"] = _topologyOptions.CommandExchange;

        arguments["x-dead-letter-routing-key"] = _topologyOptions.ConversionRequestedRoutingKey;

        await DeclareQueueAsync(
                channel,
                queueName,
                arguments,
                cancellationToken)
            .ConfigureAwait(false);

        await channel
            .QueueBindAsync(
                queue: queueName,
                exchange: _topologyOptions.RetryExchange,
                routingKey: routingKey,
                arguments: null,
                noWait: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug(
            "RabbitMQ retry queue declared and bound. " +
            "Queue: {Queue}, RoutingKey: {RoutingKey}, " +
            "DelaySeconds: {DelaySeconds}",
            queueName,
            routingKey,
            delaySeconds);
    }

    private static async Task DeclareQueueAsync(
        IChannel channel,
        string queueName,
        IDictionary<string, object?>? arguments,
        CancellationToken cancellationToken)
    {
        await channel
            .QueueDeclareAsync(
                queue: queueName,
                durable: Durable,
                exclusive: Exclusive,
                autoDelete: AutoDelete,
                arguments: arguments,
                passive: false,
                noWait: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private Dictionary<string, object?>
        CreateQueueTypeArguments()
    {
        Dictionary<string, object?> arguments =
            new(StringComparer.Ordinal);

        if (_topologyOptions.QueueType ==
            RabbitMqQueueType.Quorum)
        {
            arguments["x-queue-type"] = "quorum";
        }

        return arguments;
    }

    private static IDictionary<string, object?>? NullWhenEmpty(
        Dictionary<string, object?> arguments)
    {
        return arguments.Count == 0
            ? null
            : arguments;
    }
}