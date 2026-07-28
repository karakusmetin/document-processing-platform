using Queue.Messaging.RabbitMq.Configuration;
using Queue.Messaging.RabbitMq.Publishing;
using Queue.Messaging.RabbitMq.Topology;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Queue.Messaging.Abstractions;
using Queue.Messaging.RabbitMq.Compatibility;

namespace Queue.Messaging.RabbitMq.Retrying;

internal sealed class RabbitMqMessageRetryScheduler :
    IMessageRetryScheduler
{
    private readonly IRabbitMqPublisher _rabbitMqPublisher;
    private readonly IRabbitMqMessageRouteResolver _routeResolver;
    private readonly RabbitMqRetryOptions _retryOptions;
    private readonly RabbitMqPublisherOptions _publisherOptions;
    private readonly ILogger<RabbitMqMessageRetryScheduler> _logger;

    public RabbitMqMessageRetryScheduler(
    IRabbitMqPublisher rabbitMqPublisher,
    IRabbitMqMessageRouteResolver routeResolver,
    IOptions<RabbitMqRetryOptions> retryOptions,
    IOptions<RabbitMqPublisherOptions> publisherOptions,
    ILogger<RabbitMqMessageRetryScheduler> logger)
    {
        Guard.NotNull(rabbitMqPublisher, nameof(rabbitMqPublisher));
        Guard.NotNull(routeResolver, nameof(routeResolver));
        Guard.NotNull(retryOptions, nameof(retryOptions));
        Guard.NotNull(publisherOptions, nameof(publisherOptions));
        Guard.NotNull(logger, nameof(logger));

        _rabbitMqPublisher = rabbitMqPublisher;
        _routeResolver = routeResolver;
        _retryOptions = retryOptions.Value;
        _publisherOptions = publisherOptions.Value;
        _logger = logger;
    }

    public async Task ScheduleRetryAsync<TMessage>(
        MessageEnvelope<TMessage> originalEnvelope,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(originalEnvelope, nameof(originalEnvelope));

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

        int delaySeconds =
            ResolveDelaySeconds(delay);

        int nextAttempt =
            checked(originalEnvelope.Attempt + 1);

        MessageEnvelope<TMessage> retryEnvelope =
            MessageEnvelope<TMessage>.Create(
                payload:
                    originalEnvelope.Payload,

                messageType:
                    originalEnvelope.MessageType,

                messageVersion:
                    originalEnvelope.MessageVersion,

                /*
                 * Retry mesajını yeniden yayınlayan uygulama
                 * Worker'dır.
                 */
                producer:
                    _publisherOptions.ProducerName,

                correlationId:
                    originalEnvelope.CorrelationId,

                /*
                 * Yeni mesajın sebebi önceki fiziksel mesajdır.
                 */
                causationId:
                    originalEnvelope.MessageId.ToString("D"),

                attempt:
                    nextAttempt);

        RabbitMqMessageRoute route = _routeResolver.Resolve<TMessage>();

        if (string.IsNullOrWhiteSpace(
                route.RetryExchange) ||
            string.IsNullOrWhiteSpace(
                route.RetryRoutingKeyPrefix))
        {
            throw new InvalidOperationException(
                $"RabbitMQ delayed retry is not configured for CLR " +
                $"message type '{typeof(TMessage).FullName}'.");
        }

        string retryExchange = Guard.NotNullOrWhiteSpace(route.RetryExchange, nameof(route.RetryExchange));

        string retryRoutingKeyPrefix =
            Guard.NotNullOrWhiteSpace(
                route.RetryRoutingKeyPrefix,
                nameof(route.RetryRoutingKeyPrefix));

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
         * Bu çağrı publisher confirm alınmadan tamamlanmaz.
         */
        await _rabbitMqPublisher
            .PublishAsync(
                retryEnvelope,
                destination,
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "RabbitMQ retry message was scheduled and confirmed. " +
            "OriginalMessageId: {OriginalMessageId}, " +
            "RetryMessageId: {RetryMessageId}, " +
            "CorrelationId: {CorrelationId}, " +
            "CurrentAttempt: {CurrentAttempt}, " +
            "NextAttempt: {NextAttempt}, " +
            "DelaySeconds: {DelaySeconds}, " +
            "RoutingKey: {RoutingKey}",
            originalEnvelope.MessageId,
            retryEnvelope.MessageId,
            retryEnvelope.CorrelationId,
            originalEnvelope.Attempt,
            retryEnvelope.Attempt,
            delaySeconds,
            retryRoutingKey);
    }

    private int ResolveDelaySeconds(
        TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delay),
                delay,
                "Retry delay must be greater than zero.");
        }

        if (delay.Ticks %
            TimeSpan.TicksPerSecond != 0)
        {
            throw new ArgumentException(
                "Retry delay must contain a whole number of seconds.",
                nameof(delay));
        }

        long totalSeconds =
            delay.Ticks /
            TimeSpan.TicksPerSecond;

        int delaySeconds =
            checked((int)totalSeconds);

        /*
         * Topology yalnızca appsettings'teki delay'ler için
         * retry queue oluşturdu.
         *
         * Declare edilmemiş bir routing key'e publish edilmesine
         * izin vermiyoruz.
         */
        if (!_retryOptions.DelaySeconds.Contains(
                delaySeconds))
        {
            throw new InvalidOperationException(
                $"Retry delay '{delaySeconds}' seconds is not " +
                "configured in RabbitMqRetryOptions.");
        }

        return delaySeconds;
    }
}