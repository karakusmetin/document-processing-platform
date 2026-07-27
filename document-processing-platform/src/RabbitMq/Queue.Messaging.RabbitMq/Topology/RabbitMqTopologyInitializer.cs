using Queue.Messaging.RabbitMq.Channels;
using Queue.Messaging.RabbitMq.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Queue.Messaging.RabbitMq.Topology;

internal sealed class RabbitMqTopologyInitializer :
    IRabbitMqTopologyInitializer
{
    private readonly IRabbitMqChannelFactory _channelFactory;

    private readonly IReadOnlyList<
        IRabbitMqTopologyDefinition> _definitions;

    private readonly RabbitMqTopologyOptions _options;

    private readonly ILogger<
        RabbitMqTopologyInitializer> _logger;

    public RabbitMqTopologyInitializer(
        IRabbitMqChannelFactory channelFactory,
        IEnumerable<IRabbitMqTopologyDefinition> definitions,
        IOptions<RabbitMqTopologyOptions> options,
        ILogger<RabbitMqTopologyInitializer> logger)
    {
        ArgumentNullException.ThrowIfNull(channelFactory);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _channelFactory = channelFactory;
        _options = options.Value;
        _logger = logger;

        IRabbitMqTopologyDefinition[] definitionArray =
            definitions.ToArray();

        ValidateDefinitions(
            definitionArray);

        _definitions =
            definitionArray
                .OrderBy(
                    static definition =>
                        definition.Order)
                .ThenBy(
                    static definition =>
                        definition.Name,
                    StringComparer.Ordinal)
                .ToArray();
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken)
    {
        if (_definitions.Count == 0)
        {
            _logger.LogWarning(
                "RabbitMQ topology initialization was requested, " +
                "but no topology definitions were registered.");

            return;
        }

        _logger.LogInformation(
            "RabbitMQ topology initialization started. " +
            "DefinitionCount: {DefinitionCount}, " +
            "DefaultQueueType: {DefaultQueueType}",
            _definitions.Count,
            _options.QueueType);

        IChannel? channel = null;

        try
        {
            channel =
                await _channelFactory
                    .CreateChannelAsync(
                        RabbitMqChannelPurpose.Topology,
                        cancellationToken)
                    .ConfigureAwait(false);

            IRabbitMqTopologyBuilder builder =
                new RabbitMqTopologyBuilder(
                    channel,
                    _options,
                    _logger);

            foreach (
                IRabbitMqTopologyDefinition definition
                in _definitions)
            {
                _logger.LogInformation(
                    "Declaring RabbitMQ topology definition. " +
                    "DefinitionName: {DefinitionName}, " +
                    "Order: {Order}",
                    definition.Name,
                    definition.Order);

                await definition
                    .DeclareAsync(
                        builder,
                        cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "RabbitMQ topology definition declared. " +
                    "DefinitionName: {DefinitionName}",
                    definition.Name);
            }

            _logger.LogInformation(
                "RabbitMQ topology initialization completed. " +
                "DefinitionCount: {DefinitionCount}",
                _definitions.Count);
        }
        finally
        {
            if (channel is not null)
            {
                await DisposeChannelAsync(channel)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task DisposeChannelAsync(
        IChannel channel)
    {
        try
        {
            if (channel.IsOpen)
            {
                await channel
                    .CloseAsync(
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }

            await channel
                .DisposeAsync()
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "An error occurred while disposing the RabbitMQ " +
                "topology channel.");
        }
    }

    private static void ValidateDefinitions(
        IEnumerable<IRabbitMqTopologyDefinition> definitions)
    {
        IRabbitMqTopologyDefinition[] definitionArray =
            definitions.ToArray();

        IRabbitMqTopologyDefinition? invalidDefinition =
            definitionArray.FirstOrDefault(
                static definition =>
                    string.IsNullOrWhiteSpace(
                        definition.Name));

        if (invalidDefinition is not null)
        {
            throw new InvalidOperationException(
                "RabbitMQ topology definition name cannot be empty.");
        }

        string[] duplicateNames =
            definitionArray
                .GroupBy(
                    static definition =>
                        definition.Name,
                    StringComparer.Ordinal)
                .Where(
                    static group =>
                        group.Count() > 1)
                .Select(
                    static group =>
                        group.Key)
                .ToArray();

        if (duplicateNames.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Multiple RabbitMQ topology definitions were " +
            "registered with the same name. Duplicate names: " +
            string.Join(", ", duplicateNames));
    }
}