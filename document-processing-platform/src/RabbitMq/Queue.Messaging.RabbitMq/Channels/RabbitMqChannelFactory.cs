using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Queue.Messaging.RabbitMq.Compatibility;
using Queue.Messaging.RabbitMq.Configuration;
using Queue.Messaging.RabbitMq.Connection;
using Queue.Messaging.RabbitMq.Consuming;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Queue.Messaging.RabbitMq.Channels;

internal sealed class RabbitMqChannelFactory : IRabbitMqChannelFactory
{
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly RabbitMqEffectiveConsumerOptions _consumerOptions;
    private readonly ILogger<RabbitMqChannelFactory> _logger;

    public RabbitMqChannelFactory(
        IRabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMqEffectiveConsumerOptions> consumerOptions,
        ILogger<RabbitMqChannelFactory> logger)
    {
        Guard.NotNull(connectionProvider,nameof(connectionProvider));
        Guard.NotNull(consumerOptions, nameof(consumerOptions));
        Guard.NotNull(logger,nameof(logger));

        _connectionProvider = connectionProvider;
        _consumerOptions = consumerOptions.Value;
        _logger = logger;
    }

    public async Task<IChannel> CreateChannelAsync(
        RabbitMqChannelPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        IConnection connection =
            await _connectionProvider
                .GetConnectionAsync(cancellationToken)
                .ConfigureAwait(false);

        CreateChannelOptions channelOptions =
            CreateOptions(purpose);

        _logger.LogDebug(
            "Creating RabbitMQ channel. Purpose: {ChannelPurpose}",
            purpose);

        try
        {
            IChannel channel =
                await connection
                    .CreateChannelAsync(
                        channelOptions,
                        cancellationToken)
                    .ConfigureAwait(false);

            RegisterChannelEvents(channel, purpose);

            _logger.LogInformation(
                "RabbitMQ channel created. Purpose: {ChannelPurpose}, " +
                "ChannelNumber: {ChannelNumber}",
                purpose,
                channel.ChannelNumber);

            return channel;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "RabbitMQ channel creation was cancelled. " +
                "Purpose: {ChannelPurpose}",
                purpose);

            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "RabbitMQ channel could not be created. " +
                "Purpose: {ChannelPurpose}",
                purpose);

            throw;
        }
    }

    private CreateChannelOptions CreateOptions(
    RabbitMqChannelPurpose purpose)
    {
        return purpose switch
        {
            RabbitMqChannelPurpose.Publisher =>
                CreatePublisherOptions(),

            RabbitMqChannelPurpose.Consumer =>
                CreateConsumerOptions(),

            RabbitMqChannelPurpose.Topology =>
                CreateTopologyOptions(),

            _ => throw new ArgumentOutOfRangeException(
                nameof(purpose),
                purpose,
                "Unsupported RabbitMQ channel purpose.")
        };
    }

    private static CreateChannelOptions CreatePublisherOptions()
    {
        return new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true,
            outstandingPublisherConfirmationsRateLimiter: null,
            consumerDispatchConcurrency: null);
    }

    private static CreateChannelOptions CreateConsumerOptions()
    {
        return new CreateChannelOptions(
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false,
            outstandingPublisherConfirmationsRateLimiter: null,
            consumerDispatchConcurrency: 1);
    }

    private static CreateChannelOptions CreateTopologyOptions()
    {
        return new CreateChannelOptions(
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false,
            outstandingPublisherConfirmationsRateLimiter: null,
            consumerDispatchConcurrency: null);
    }

    private void RegisterChannelEvents(
        IChannel channel,
        RabbitMqChannelPurpose purpose)
    {
        channel.ChannelShutdownAsync +=
            (_, eventArgs) =>
                HandleChannelShutdownAsync(
                    purpose,
                    eventArgs);

        channel.CallbackExceptionAsync +=
            (_, eventArgs) =>
                HandleCallbackExceptionAsync(
                    purpose,
                    eventArgs);
    }

    private Task HandleChannelShutdownAsync(
        RabbitMqChannelPurpose purpose,
        ShutdownEventArgs eventArgs)
    {
        _logger.LogWarning(
            "RabbitMQ channel shut down. Purpose: {ChannelPurpose}, " +
            "Initiator: {Initiator}, ReplyCode: {ReplyCode}, " +
            "ReplyText: {ReplyText}",
            purpose,
            eventArgs.Initiator,
            eventArgs.ReplyCode,
            eventArgs.ReplyText);

        return Task.CompletedTask;
    }

    private Task HandleCallbackExceptionAsync(
        RabbitMqChannelPurpose purpose,
        CallbackExceptionEventArgs eventArgs)
    {
        _logger.LogError(
            eventArgs.Exception,
            "RabbitMQ channel callback failed. " +
            "Purpose: {ChannelPurpose}",
            purpose);

        return Task.CompletedTask;
    }
}