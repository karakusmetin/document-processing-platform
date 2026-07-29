using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Queue.Messaging.Abstractions;
using Queue.Messaging.RabbitMq.Compatibility;
using Queue.Messaging.RabbitMq.Configuration;
using Queue.Messaging.RabbitMq.Publishing;
using Queue.Messaging.RabbitMq.Topology;

namespace Queue.Messaging.RabbitMq.Retrying;

internal sealed class RabbitMqMessageRetryScheduler :
    IMessageRetryScheduler
{
    private readonly IRabbitMqPublisher _rabbitMqPublisher;

    private readonly IRabbitMqMessageRouteResolver
        _routeResolver;

    private readonly RabbitMqRetryOptions
        _globalRetryOptions;

    private readonly RabbitMqPublisherOptions
        _publisherOptions;

    private readonly ILogger<RabbitMqMessageRetryScheduler>
        _logger;

    public RabbitMqMessageRetryScheduler(
        IRabbitMqPublisher rabbitMqPublisher,
        IRabbitMqMessageRouteResolver routeResolver,
        IOptions<RabbitMqRetryOptions> retryOptions,
        IOptions<RabbitMqPublisherOptions> publisherOptions,
        ILogger<RabbitMqMessageRetryScheduler> logger)
    {
        Guard.NotNull(
            rabbitMqPublisher,
            nameof(rabbitMqPublisher));

        Guard.NotNull(
            routeResolver,
            nameof(routeResolver));

        Guard.NotNull(
            retryOptions,
            nameof(retryOptions));

        Guard.NotNull(
            publisherOptions,
            nameof(publisherOptions));

        Guard.NotNull(
            logger,
            nameof(logger));

        _rabbitMqPublisher =
            rabbitMqPublisher;

        _routeResolver =
            routeResolver;

        _globalRetryOptions =
            retryOptions.Value;

        _publisherOptions =
            publisherOptions.Value;

        _logger =
            logger;
    }

    public async Task ScheduleRetryAsync<TMessage>(
        MessageEnvelope<TMessage> originalEnvelope,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(
            originalEnvelope,
            nameof(originalEnvelope));

        if (originalEnvelope.Payload is null)
        {
            throw new ArgumentException(
                "Original message payload cannot be null.",
                nameof(originalEnvelope));
        }

        if (originalEnvelope.Attempt < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(originalEnvelope),
                originalEnvelope.Attempt,
                "Original message attempt must be greater than zero.");
        }

        RabbitMqMessageRoute route =
            _routeResolver.Resolve<TMessage>();

        string retryExchange =
            ResolveRetryExchange<TMessage>(
                route);

        string retryRoutingKeyPrefix =
            ResolveRetryRoutingKeyPrefix<TMessage>(
                route);

        RabbitMqEffectiveRetryPolicy retryPolicy =
            RabbitMqEffectiveRetryPolicy.Resolve(
                _globalRetryOptions,
                route.RetryMaximumAttempts,
                route.RetryDelaySeconds);

        int delaySeconds =
            ResolveDelaySeconds(
                delay,
                originalEnvelope.Attempt,
                retryPolicy);

        int nextAttempt =
            checked(
                originalEnvelope.Attempt + 1);

        MessageEnvelope<TMessage> retryEnvelope =
            MessageEnvelope<TMessage>.Create(
                payload:
                    originalEnvelope.Payload,

                messageType:
                    originalEnvelope.MessageType,

                messageVersion:
                    originalEnvelope.MessageVersion,

                producer:
                    _publisherOptions.ProducerName,

                correlationId:
                    originalEnvelope.CorrelationId,

                /*
                 * Retry mesajı yeni bir fiziksel mesajdır.
                 * Önceki MessageId causation zincirine alınır.
                 */
                causationId:
                    originalEnvelope.MessageId.ToString("D"),

                attempt:
                    nextAttempt);

        string retryRoutingKey =
            RabbitMqTopologyNameBuilder
                .GetRetryRoutingKey(
                    retryRoutingKeyPrefix,
                    delaySeconds);

        RabbitMqPublishDestination destination =
            new(
                retryExchange,
                retryRoutingKey);

        /*
         * Publish çağrısı publisher confirmation alınmadan
         * tamamlanmaz.
         *
         * Handler bu çağrı tamamlandıktan sonra orijinal
         * mesaja ACK sonucu dönebilir.
         */
        await _rabbitMqPublisher
            .PublishAsync(
                retryEnvelope,
                destination,
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "RabbitMQ retry message was scheduled and confirmed. " +
            "MessageClrType: {MessageClrType}, " +
            "OriginalMessageId: {OriginalMessageId}, " +
            "RetryMessageId: {RetryMessageId}, " +
            "CorrelationId: {CorrelationId}, " +
            "CurrentAttempt: {CurrentAttempt}, " +
            "NextAttempt: {NextAttempt}, " +
            "MaximumAttempts: {MaximumAttempts}, " +
            "DelaySeconds: {DelaySeconds}, " +
            "RoutingKey: {RoutingKey}",
            typeof(TMessage).FullName,
            originalEnvelope.MessageId,
            retryEnvelope.MessageId,
            retryEnvelope.CorrelationId,
            originalEnvelope.Attempt,
            retryEnvelope.Attempt,
            retryPolicy.MaximumAttempts,
            delaySeconds,
            retryRoutingKey);
    }

    private static string ResolveRetryExchange<TMessage>(
    RabbitMqMessageRoute route)
    {
        string? retryExchange =
            route.RetryExchange;

        if (retryExchange is null)
        {
            throw new InvalidOperationException(
                "RabbitMQ delayed retry exchange is not " +
                "configured for CLR message type " +
                $"'{typeof(TMessage).FullName}'.");
        }

        retryExchange =
            retryExchange.Trim();

        if (retryExchange.Length == 0)
        {
            throw new InvalidOperationException(
                "RabbitMQ delayed retry exchange is not " +
                "configured for CLR message type " +
                $"'{typeof(TMessage).FullName}'.");
        }

        return retryExchange;
    }

    private static string ResolveRetryRoutingKeyPrefix<TMessage>(
        RabbitMqMessageRoute route)
    {
        string? retryRoutingKeyPrefix =
            route.RetryRoutingKeyPrefix;

        if (retryRoutingKeyPrefix is null)
        {
            throw new InvalidOperationException(
                "RabbitMQ delayed retry routing key prefix is " +
                "not configured for CLR message type " +
                $"'{typeof(TMessage).FullName}'.");
        }

        retryRoutingKeyPrefix =
            retryRoutingKeyPrefix.Trim();

        if (retryRoutingKeyPrefix.Length == 0)
        {
            throw new InvalidOperationException(
                "RabbitMQ delayed retry routing key prefix is " +
                "not configured for CLR message type " +
                $"'{typeof(TMessage).FullName}'.");
        }

        return retryRoutingKeyPrefix;
    }

    private static int ResolveDelaySeconds(
        TimeSpan requestedDelay,
        int currentAttempt,
        RabbitMqEffectiveRetryPolicy retryPolicy)
    {
        Guard.NotNull(
            retryPolicy,
            nameof(retryPolicy));

        if (requestedDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedDelay),
                requestedDelay,
                "Retry delay must be greater than zero.");
        }

        if (requestedDelay.Ticks %
            TimeSpan.TicksPerSecond != 0)
        {
            throw new ArgumentException(
                "Retry delay must contain a whole number of seconds.",
                nameof(requestedDelay));
        }

        long totalSeconds =
            requestedDelay.Ticks /
            TimeSpan.TicksPerSecond;

        int requestedDelaySeconds =
            checked((int)totalSeconds);

        int expectedDelaySeconds =
            retryPolicy.GetDelaySecondsForCurrentAttempt(
                currentAttempt);

        /*
         * Caller'ın topology içerisinde bulunmayan veya mevcut
         * attempt'e ait olmayan bir gecikme seçmesine izin
         * vermiyoruz.
         */
        if (requestedDelaySeconds !=
            expectedDelaySeconds)
        {
            throw new InvalidOperationException(
                "Requested RabbitMQ retry delay does not match " +
                "the effective retry policy. " +
                $"Current attempt: '{currentAttempt}', " +
                $"expected delay: '{expectedDelaySeconds}' seconds, " +
                $"requested delay: '{requestedDelaySeconds}' seconds.");
        }

        return requestedDelaySeconds;
    }
}