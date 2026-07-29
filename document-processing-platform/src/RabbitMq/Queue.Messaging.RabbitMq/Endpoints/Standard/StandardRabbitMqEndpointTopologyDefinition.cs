using Microsoft.Extensions.Options;
using Queue.Messaging.RabbitMq.Compatibility;
using Queue.Messaging.RabbitMq.Configuration;
using Queue.Messaging.RabbitMq.Retrying;
using Queue.Messaging.RabbitMq.Topology;

namespace Queue.Messaging.RabbitMq.Endpoints.Standard;

/// <summary>
/// Tek bir standard endpoint için command, retry ve
/// dead-letter topology'sini oluşturur.
/// </summary>
internal sealed class
    StandardRabbitMqEndpointTopologyDefinition<TMessage> :
    IRabbitMqTopologyDefinition
{
    private readonly
        StandardRabbitMqEndpointRegistration<TMessage>
        _registration;

    private readonly RabbitMqRetryOptions
        _globalRetryOptions;

    public StandardRabbitMqEndpointTopologyDefinition(
        StandardRabbitMqEndpointRegistration<TMessage>
            registration,
        IOptions<RabbitMqRetryOptions> retryOptions)
    {
        Guard.NotNull(
            registration,
            nameof(registration));

        Guard.NotNull(
            retryOptions,
            nameof(retryOptions));

        _registration =
            registration;

        _globalRetryOptions =
            retryOptions.Value;
    }

    public string Name =>
        $"standard-endpoint:{_registration.EndpointName}";

    public int Order =>
        _registration.TopologyOrder;

    public async Task DeclareAsync(
        IRabbitMqTopologyBuilder builder,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(
            builder,
            nameof(builder));

        StandardRabbitMqEndpointNames names =
            _registration.Names;

        RabbitMqEffectiveRetryPolicy retryPolicy =
            RabbitMqEffectiveRetryPolicy.Resolve(
                _globalRetryOptions,
                _registration.MaximumAttempts,
                _registration.DelaySeconds);

        /*
         * Önce bütün exchange'leri oluşturuyoruz.
         */
        await builder
            .DeclareExchangeAsync(
                name:
                    names.ExchangeName,

                type:
                    RabbitMqExchangeTypes.Direct,

                durable:
                    true,

                autoDelete:
                    false,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        await builder
            .DeclareExchangeAsync(
                name:
                    names.RetryExchangeName,

                type:
                    RabbitMqExchangeTypes.Direct,

                durable:
                    true,

                autoDelete:
                    false,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        await builder
            .DeclareExchangeAsync(
                name:
                    names.DeadLetterExchangeName,

                type:
                    RabbitMqExchangeTypes.Direct,

                durable:
                    true,

                autoDelete:
                    false,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        await DeclareDeadLetterTopologyAsync(
                builder,
                names,
                cancellationToken)
            .ConfigureAwait(false);

        await DeclareMainTopologyAsync(
                builder,
                names,
                cancellationToken)
            .ConfigureAwait(false);

        await DeclareRetryTopologyAsync(
                builder,
                names,
                retryPolicy,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task DeclareMainTopologyAsync(
        IRabbitMqTopologyBuilder builder,
        StandardRabbitMqEndpointNames names,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> queueArguments =
            new()
            {
                [
                    RabbitMqTopologyArgumentNames
                        .DeadLetterExchange
                ] =
                    names.DeadLetterExchangeName,

                [
                    RabbitMqTopologyArgumentNames
                        .DeadLetterRoutingKey
                ] =
                    names.DeadLetterRoutingKey
            };

        await builder
            .DeclareQueueAsync(
                name:
                    names.QueueName,

                queueType:
                    _registration.QueueType,

                durable:
                    true,

                exclusive:
                    false,

                autoDelete:
                    false,

                arguments:
                    queueArguments,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        await builder
            .BindQueueAsync(
                queue:
                    names.QueueName,

                exchange:
                    names.ExchangeName,

                routingKey:
                    names.RoutingKey,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task DeclareDeadLetterTopologyAsync(
        IRabbitMqTopologyBuilder builder,
        StandardRabbitMqEndpointNames names,
        CancellationToken cancellationToken)
    {
        await builder
            .DeclareQueueAsync(
                name:
                    names.DeadLetterQueueName,

                queueType:
                    _registration.QueueType,

                durable:
                    true,

                exclusive:
                    false,

                autoDelete:
                    false,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);

        await builder
            .BindQueueAsync(
                queue:
                    names.DeadLetterQueueName,

                exchange:
                    names.DeadLetterExchangeName,

                routingKey:
                    names.DeadLetterRoutingKey,

                cancellationToken:
                    cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task DeclareRetryTopologyAsync(
        IRabbitMqTopologyBuilder builder,
        StandardRabbitMqEndpointNames names,
        RabbitMqEffectiveRetryPolicy retryPolicy,
        CancellationToken cancellationToken)
    {
        foreach (
            int delaySeconds
            in retryPolicy.DelaySeconds)
        {
            string retryQueueName =
                RabbitMqTopologyNameBuilder
                    .GetRetryQueueName(
                        names.RetryQueueNamePrefix,
                        delaySeconds);

            string retryRoutingKey =
                RabbitMqTopologyNameBuilder
                    .GetRetryRoutingKey(
                        names.RetryRoutingKeyPrefix,
                        delaySeconds);

            long messageTtlMilliseconds =
                checked(
                    (long)delaySeconds *
                    1000L);

            Dictionary<string, object?> queueArguments =
                new()
                {
                    [
                        RabbitMqTopologyArgumentNames
                            .MessageTtl
                    ] =
                        messageTtlMilliseconds,

                    [
                        RabbitMqTopologyArgumentNames
                            .DeadLetterExchange
                    ] =
                        names.ExchangeName,

                    [
                        RabbitMqTopologyArgumentNames
                            .DeadLetterRoutingKey
                    ] =
                        names.RoutingKey
                };

            await builder
                .DeclareQueueAsync(
                    name:
                        retryQueueName,

                    queueType:
                        _registration.QueueType,

                    durable:
                        true,

                    exclusive:
                        false,

                    autoDelete:
                        false,

                    arguments:
                        queueArguments,

                    cancellationToken:
                        cancellationToken)
                .ConfigureAwait(false);

            await builder
                .BindQueueAsync(
                    queue:
                        retryQueueName,

                    exchange:
                        names.RetryExchangeName,

                    routingKey:
                        retryRoutingKey,

                    cancellationToken:
                        cancellationToken)
                .ConfigureAwait(false);
        }
    }
}