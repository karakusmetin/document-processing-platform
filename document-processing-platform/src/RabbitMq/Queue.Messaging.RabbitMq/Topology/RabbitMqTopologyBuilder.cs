using Queue.Messaging.RabbitMq.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Queue.Messaging.RabbitMq.Compatibility;

namespace Queue.Messaging.RabbitMq.Topology;

internal sealed class RabbitMqTopologyBuilder :
    IRabbitMqTopologyBuilder
{
    private readonly IChannel _channel;
    private readonly RabbitMqTopologyOptions _options;
    private readonly ILogger _logger;

    public RabbitMqTopologyBuilder(
        IChannel channel,
        RabbitMqTopologyOptions options,
        ILogger logger)
    {
        Guard.NotNull(channel, nameof(channel));
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(logger, nameof(logger));

        _channel = channel;
        _options = options;
        _logger = logger;
    }

    public async Task DeclareExchangeAsync(
        string name,
        string type,
        bool durable = true,
        bool autoDelete = false,
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.NotNullOrWhiteSpace(type, nameof(type));

        IDictionary<string, object?>? effectiveArguments =
            CopyArguments(arguments);

        await _channel
            .ExchangeDeclareAsync(
                exchange:
                    name,

                type:
                    type,

                durable:
                    durable,

                autoDelete:
                    autoDelete,

                arguments:
                    effectiveArguments,

                noWait:
                    false,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug(
            "RabbitMQ exchange declared. " +
            "Exchange: {Exchange}, " +
            "Type: {ExchangeType}, " +
            "Durable: {Durable}, " +
            "AutoDelete: {AutoDelete}",
            name,
            type,
            durable,
            autoDelete);
    }

    public async Task DeclareQueueAsync(
        string name,
        RabbitMqQueueType? queueType = null,
        bool durable = true,
        bool exclusive = false,
        bool autoDelete = false,
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));

        RabbitMqQueueType effectiveQueueType =
            queueType ??
            _options.QueueType;

        ValidateQueueType(
            effectiveQueueType,
            durable,
            exclusive,
            autoDelete);

        IDictionary<string, object?>? effectiveArguments =
            CreateQueueArguments(
                effectiveQueueType,
                arguments);

        await _channel
            .QueueDeclareAsync(
                queue:
                    name,

                durable:
                    durable,

                exclusive:
                    exclusive,

                autoDelete:
                    autoDelete,

                arguments:
                    effectiveArguments,

                noWait:
                    false,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug(
            "RabbitMQ queue declared. " +
            "Queue: {Queue}, " +
            "QueueType: {QueueType}, " +
            "Durable: {Durable}, " +
            "Exclusive: {Exclusive}, " +
            "AutoDelete: {AutoDelete}",
            name,
            effectiveQueueType,
            durable,
            exclusive,
            autoDelete);
    }

    public async Task BindQueueAsync(
        string queue,
        string exchange,
        string routingKey,
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(queue, nameof(queue));
        Guard.NotNullOrWhiteSpace(exchange, nameof(exchange));

        /*
         * Fanout exchange kullanımında routing key boş olabilir.
         * Bu nedenle burada ThrowIfNullOrWhiteSpace kullanmıyoruz.
         */
        Guard.NotNull(routingKey, nameof(routingKey));

        IDictionary<string, object?>? effectiveArguments =
            CopyArguments(arguments);

        await _channel
            .QueueBindAsync(
                queue:
                    queue,

                exchange:
                    exchange,

                routingKey:
                    routingKey,

                arguments:
                    effectiveArguments,

                noWait:
                    false,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug(
            "RabbitMQ queue binding declared. " +
            "Queue: {Queue}, " +
            "Exchange: {Exchange}, " +
            "RoutingKey: {RoutingKey}",
            queue,
            exchange,
            routingKey);
    }

    private static IDictionary<string, object?>?
        CreateQueueArguments(
            RabbitMqQueueType queueType,
            IReadOnlyDictionary<string, object?>? arguments)
    {
        Dictionary<string, object?> effectiveArguments =
            arguments is null
                ? new Dictionary<string, object?>()
                : arguments.ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value);

        /*
         * Queue type yönetimi topology builder'ın
         * sorumluluğundadır.
         */
        effectiveArguments.Remove(
            RabbitMqTopologyArgumentNames.QueueType);

        switch (queueType)
        {
            case RabbitMqQueueType.Classic:
                /*
                 * Classic queue için x-queue-type göndermiyoruz.
                 * Eski broker sürümleriyle uyumluluğu koruyoruz.
                 */
                break;

            case RabbitMqQueueType.Quorum:
                effectiveArguments[
                    RabbitMqTopologyArgumentNames.QueueType] =
                    "quorum";

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(queueType),
                    queueType,
                    "Unsupported RabbitMQ queue type.");
        }

        return effectiveArguments.Count == 0
            ? null
            : effectiveArguments;
    }

    private static IDictionary<string, object?>?
        CopyArguments(
            IReadOnlyDictionary<string, object?>? arguments)
    {
        if (arguments is null ||
            arguments.Count == 0)
        {
            return null;
        }

        return arguments.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value);
    }

    private static void ValidateQueueType(
        RabbitMqQueueType queueType,
        bool durable,
        bool exclusive,
        bool autoDelete)
    {
        if (!EnumCompatibility.IsDefined(queueType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(queueType),
                queueType,
                "Unsupported RabbitMQ queue type.");
        }

        if (queueType == RabbitMqQueueType.Quorum &&
            (!durable || exclusive || autoDelete))
        {
            throw new InvalidOperationException(
                "Quorum queues must be durable, non-exclusive " +
                "and non-auto-delete.");
        }
    }
}