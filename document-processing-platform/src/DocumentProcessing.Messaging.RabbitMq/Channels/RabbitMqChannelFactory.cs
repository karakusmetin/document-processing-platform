using DocumentProcessing.Messaging.RabbitMq.Configuration;
using DocumentProcessing.Messaging.RabbitMq.Connection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DocumentProcessing.Messaging.RabbitMq.Channels;

internal sealed class RabbitMqChannelFactory : IRabbitMqChannelFactory
{
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly RabbitMqPublisherOptions _publisherOptions;
    private readonly RabbitMqConsumerOptions _consumerOptions;
    private readonly ILogger<RabbitMqChannelFactory> _logger;

    public RabbitMqChannelFactory(
        IRabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMqPublisherOptions> publisherOptions,
        IOptions<RabbitMqConsumerOptions> consumerOptions,
        ILogger<RabbitMqChannelFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(connectionProvider);
        ArgumentNullException.ThrowIfNull(publisherOptions);
        ArgumentNullException.ThrowIfNull(consumerOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _connectionProvider = connectionProvider;
        _publisherOptions = publisherOptions.Value;
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

    private CreateChannelOptions CreatePublisherOptions()
    {
        return new CreateChannelOptions(
            publisherConfirmationsEnabled:
                _publisherOptions.PublisherConfirmationsEnabled,
            publisherConfirmationTrackingEnabled:
                _publisherOptions
                    .PublisherConfirmationTrackingEnabled,
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