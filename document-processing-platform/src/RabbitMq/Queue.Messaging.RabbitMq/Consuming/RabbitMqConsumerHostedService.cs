using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Queue.Messaging.RabbitMq.Channels;
using Queue.Messaging.RabbitMq.Compatibility;
using Queue.Messaging.RabbitMq.Configuration;
using Queue.Messaging.RabbitMq.Serialization;

namespace Queue.Messaging.RabbitMq.Consuming;

internal sealed class RabbitMqConsumerHostedService<TMessage> :
    IHostedService
{
    private readonly IRabbitMqChannelFactory _channelFactory;
    private readonly IMessageSerializer _messageSerializer;
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly RabbitMqConsumerDefinition<TMessage>
        _definition;

    private readonly RabbitMqEffectiveConsumerOptions
        _effectiveOptions;

    private readonly ILogger<
        RabbitMqConsumerHostedService<TMessage>> _logger;

    private readonly List<
        RabbitMqConsumerInstance<TMessage>> _instances =
        [];

    private int _started;
    private int _stopped;

    public RabbitMqConsumerHostedService(
        IRabbitMqChannelFactory channelFactory,
        IMessageSerializer messageSerializer,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqConsumerOptions> consumerOptions,
        IOptions<RabbitMqConsumerDefinition<TMessage>> definition,
        ILogger<RabbitMqConsumerHostedService<TMessage>> logger)
    {
        Guard.NotNull(
            channelFactory,
            nameof(channelFactory));

        Guard.NotNull(
            messageSerializer,
            nameof(messageSerializer));

        Guard.NotNull(
            scopeFactory,
            nameof(scopeFactory));

        Guard.NotNull(
            consumerOptions,
            nameof(consumerOptions));

        Guard.NotNull(
            definition,
            nameof(definition));

        Guard.NotNull(
            logger,
            nameof(logger));

        _channelFactory =
            channelFactory;

        _messageSerializer =
            messageSerializer;

        _scopeFactory =
            scopeFactory;

        _definition =
            definition.Value;

        _effectiveOptions =
            RabbitMqEffectiveConsumerOptions.Resolve(
                consumerOptions.Value,
                _definition);

        _logger =
            logger;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(
                ref _started,
                1) == 1)
        {
            return;
        }

        _logger.LogInformation(
            "Starting RabbitMQ consumer hosted service. " +
            "MessageClrType: {MessageClrType}, " +
            "Queue: {Queue}, " +
            "ConcurrentConsumerCount: {ConcurrentConsumerCount}, " +
            "PrefetchCountPerConsumer: {PrefetchCount}",
            typeof(TMessage).FullName,
            _definition.QueueName,
            _effectiveOptions.ConcurrentConsumerCount,
            _effectiveOptions.PrefetchCount);

        try
        {
            for (int instanceNumber = 1;
                 instanceNumber <=
                 _effectiveOptions
                     .ConcurrentConsumerCount;
                 instanceNumber++)
            {
                RabbitMqConsumerInstance<TMessage> instance =
                    new(
                        instanceNumber:
                            instanceNumber,

                        channelFactory:
                            _channelFactory,

                        messageSerializer:
                            _messageSerializer,

                        scopeFactory:
                            _scopeFactory,

                        consumerOptions:
                            _effectiveOptions,

                        definition:
                            _definition,

                        logger:
                            _logger);

                await instance
                    .StartAsync(cancellationToken)
                    .ConfigureAwait(false);

                _instances.Add(instance);
            }

            _logger.LogInformation(
                "RabbitMQ consumer hosted service started. " +
                "MessageClrType: {MessageClrType}, " +
                "Queue: {Queue}, " +
                "StartedInstanceCount: {StartedInstanceCount}",
                typeof(TMessage).FullName,
                _definition.QueueName,
                _instances.Count);
        }
        catch
        {
            await StopStartedInstancesAsync(
                    CancellationToken.None)
                .ConfigureAwait(false);

            throw;
        }
    }

    public async Task StopAsync(
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(
                ref _stopped,
                1) == 1)
        {
            return;
        }

        _logger.LogInformation(
            "Stopping RabbitMQ consumer hosted service. " +
            "MessageClrType: {MessageClrType}, " +
            "Queue: {Queue}, " +
            "InstanceCount: {InstanceCount}",
            typeof(TMessage).FullName,
            _definition.QueueName,
            _instances.Count);

        await StopStartedInstancesAsync(
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "RabbitMQ consumer hosted service stopped. " +
            "MessageClrType: {MessageClrType}, " +
            "Queue: {Queue}",
            typeof(TMessage).FullName,
            _definition.QueueName);
    }

    private async Task StopStartedInstancesAsync(
        CancellationToken cancellationToken)
    {
        Task[] stopTasks =
            _instances
                .Select(
                    instance =>
                        instance.StopAsync(
                            cancellationToken))
                .ToArray();

        try
        {
            await Task
                .WhenAll(stopTasks)
                .ConfigureAwait(false);
        }
        finally
        {
            foreach (
                RabbitMqConsumerInstance<TMessage> instance
                in _instances)
            {
                await instance
                    .DisposeAsync()
                    .ConfigureAwait(false);
            }

            _instances.Clear();
        }
    }
}