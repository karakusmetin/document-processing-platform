using DocumentProcessing.Messaging.RabbitMq.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DocumentProcessing.Messaging.RabbitMq.Connection;

internal sealed class RabbitMqConnectionProvider : IRabbitMqConnectionProvider
{
    private readonly RabbitMqConnectionOptions _options;
    private readonly ILogger<RabbitMqConnectionProvider> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    private IConnection? _connection;
    private bool _disposed;

    public RabbitMqConnectionProvider(
        IOptions<RabbitMqConnectionOptions> options,
        ILogger<RabbitMqConnectionProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
    }

    public bool IsConnected =>
        !_disposed &&
        _connection is { IsOpen: true };

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        IConnection? currentConnection = _connection;

        if (currentConnection is { IsOpen: true })
        {
            return currentConnection;
        }

        await _initializationLock
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            currentConnection = _connection;

            if (currentConnection is { IsOpen: true })
            {
                return currentConnection;
            }

            await DisposeConnectionAsync(currentConnection)
                .ConfigureAwait(false);

            _connection = await CreateConnectionAsync(cancellationToken)
                .ConfigureAwait(false);

            RegisterConnectionEvents(_connection);

            return _connection;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _initializationLock
            .WaitAsync(CancellationToken.None)
            .ConfigureAwait(false);

        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            IConnection? connection = _connection;
            _connection = null;

            await DisposeConnectionAsync(connection)
                .ConfigureAwait(false);
        }
        finally
        {
            _initializationLock.Release();
            _initializationLock.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private async Task<IConnection> CreateConnectionAsync(
        CancellationToken cancellationToken)
    {
        ConnectionFactory factory = CreateConnectionFactory();

        IReadOnlyList<AmqpTcpEndpoint> endpoints =
            CreateEndpoints();

        _logger.LogInformation(
            "Creating RabbitMQ connection. Hosts: {Hosts}, Port: {Port}, " +
            "VirtualHost: {VirtualHost}, ClientName: {ClientName}",
            string.Join(", ", _options.HostNames),
            _options.Port,
            _options.VirtualHost,
            _options.ClientProvidedName);

        try
        {
            IConnection connection =
                await factory.CreateConnectionAsync(
                        endpoints,
                        _options.ClientProvidedName,
                        cancellationToken)
                    .ConfigureAwait(false);

            _logger.LogInformation(
                "RabbitMQ connection established. Endpoint: {Endpoint}, " +
                "ClientName: {ClientName}",
                connection.Endpoint,
                _options.ClientProvidedName);

            return connection;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "RabbitMQ connection attempt was cancelled. " +
                "ClientName: {ClientName}",
                _options.ClientProvidedName);

            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "RabbitMQ connection could not be established. " +
                "Hosts: {Hosts}, Port: {Port}, VirtualHost: {VirtualHost}",
                string.Join(", ", _options.HostNames),
                _options.Port,
                _options.VirtualHost);

            throw;
        }
    }

    private ConnectionFactory CreateConnectionFactory()
    {
        return new ConnectionFactory
        {
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            Port = _options.Port,
            ClientProvidedName = _options.ClientProvidedName,
            AutomaticRecoveryEnabled =
                _options.AutomaticRecoveryEnabled,
            TopologyRecoveryEnabled =
                _options.TopologyRecoveryEnabled,
            NetworkRecoveryInterval =
                _options.NetworkRecoveryInterval,
            RequestedConnectionTimeout =
                _options.RequestedConnectionTimeout,
            RequestedHeartbeat =
                _options.RequestedHeartbeat
        };
    }

    private IReadOnlyList<AmqpTcpEndpoint> CreateEndpoints()
    {
        return _options.HostNames
            .Select(hostName =>
                new AmqpTcpEndpoint(
                    hostName,
                    _options.Port))
            .ToArray();
    }

    private void RegisterConnectionEvents(IConnection connection)
    {
        connection.ConnectionShutdownAsync +=
            HandleConnectionShutdownAsync;

        connection.CallbackExceptionAsync +=
            HandleCallbackExceptionAsync;

        connection.ConnectionBlockedAsync +=
            HandleConnectionBlockedAsync;

        connection.ConnectionUnblockedAsync +=
            HandleConnectionUnblockedAsync;

        connection.RecoverySucceededAsync +=
            HandleRecoverySucceededAsync;

        connection.ConnectionRecoveryErrorAsync +=
            HandleConnectionRecoveryErrorAsync;
    }

    private void UnregisterConnectionEvents(IConnection connection)
    {
        connection.ConnectionShutdownAsync -=
            HandleConnectionShutdownAsync;

        connection.CallbackExceptionAsync -=
            HandleCallbackExceptionAsync;

        connection.ConnectionBlockedAsync -=
            HandleConnectionBlockedAsync;

        connection.ConnectionUnblockedAsync -=
            HandleConnectionUnblockedAsync;

        connection.RecoverySucceededAsync -=
            HandleRecoverySucceededAsync;

        connection.ConnectionRecoveryErrorAsync -=
            HandleConnectionRecoveryErrorAsync;
    }

    private Task HandleConnectionShutdownAsync(
        object sender,
        ShutdownEventArgs eventArgs)
    {
        _logger.LogWarning(
            "RabbitMQ connection shut down. Initiator: {Initiator}, " +
            "ReplyCode: {ReplyCode}, ReplyText: {ReplyText}",
            eventArgs.Initiator,
            eventArgs.ReplyCode,
            eventArgs.ReplyText);

        return Task.CompletedTask;
    }

    private Task HandleCallbackExceptionAsync(
        object sender,
        CallbackExceptionEventArgs eventArgs)
    {
        _logger.LogError(
            eventArgs.Exception,
            "RabbitMQ connection callback failed.");

        return Task.CompletedTask;
    }

    private Task HandleConnectionBlockedAsync(
        object sender,
        ConnectionBlockedEventArgs eventArgs)
    {
        _logger.LogWarning(
            "RabbitMQ connection was blocked by the broker. " +
            "Reason: {Reason}",
            eventArgs.Reason);

        return Task.CompletedTask;
    }

    private Task HandleConnectionUnblockedAsync(
        object sender,
        AsyncEventArgs eventArgs)
    {
        _logger.LogInformation(
            "RabbitMQ connection was unblocked by the broker.");

        return Task.CompletedTask;
    }

    private Task HandleRecoverySucceededAsync(
        object sender,
        AsyncEventArgs eventArgs)
    {
        _logger.LogInformation(
            "RabbitMQ connection recovery succeeded.");

        return Task.CompletedTask;
    }

    private Task HandleConnectionRecoveryErrorAsync(
        object sender,
        ConnectionRecoveryErrorEventArgs eventArgs)
    {
        _logger.LogError(
            eventArgs.Exception,
            "RabbitMQ connection recovery failed.");

        return Task.CompletedTask;
    }

    private async Task DisposeConnectionAsync(
        IConnection? connection)
    {
        if (connection is null)
        {
            return;
        }

        try
        {
            UnregisterConnectionEvents(connection);

            if (connection.IsOpen)
            {
                await connection
                    .CloseAsync(
                        Constants.ReplySuccess,
                        "Document processing service shutdown")
                    .ConfigureAwait(false);
            }

            await connection
                .DisposeAsync()
                .ConfigureAwait(false);

            _logger.LogInformation(
                "RabbitMQ connection disposed.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "An error occurred while disposing the RabbitMQ connection.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}