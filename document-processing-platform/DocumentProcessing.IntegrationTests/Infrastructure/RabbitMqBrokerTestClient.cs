using RabbitMQ.Client;

namespace DocumentProcessing.IntegrationTests.Infrastructure;

internal static class RabbitMqBrokerTestClient
{
    public static async Task<BasicGetResult>
        WaitForMessageAsync(
            string connectionString,
            string queueName,
            TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            connectionString);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            queueName);

        ConnectionFactory factory =
            new()
            {
                Uri =
                    new Uri(connectionString),

                ClientProvidedName =
                    "integration-test-broker-verifier"
            };

        await using IConnection connection =
            await factory.CreateConnectionAsync();

        await using IChannel channel =
            await connection.CreateChannelAsync();

        using CancellationTokenSource timeoutSource =
            new(timeout);

        while (!timeoutSource.IsCancellationRequested)
        {
            BasicGetResult? result =
                await channel.BasicGetAsync(
                    queue:
                        queueName,

                    autoAck:
                        true);

            if (result is not null)
            {
                return result;
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(100),
                    timeoutSource.Token);
            }
            catch (OperationCanceledException)
                when (timeoutSource.IsCancellationRequested)
            {
                break;
            }
        }

        throw new TimeoutException(
            $"RabbitMQ queue '{queueName}' did not receive " +
            $"a message within '{timeout}'.");
    }

    public static async Task<bool> QueueContainsMessageAsync(
        string connectionString,
        string queueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            connectionString);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            queueName);

        ConnectionFactory factory =
            new()
            {
                Uri =
                    new Uri(connectionString),

                ClientProvidedName =
                    "integration-test-queue-verifier"
            };

        await using IConnection connection =
            await factory.CreateConnectionAsync();

        await using IChannel channel =
            await connection.CreateChannelAsync();

        BasicGetResult? result =
            await channel.BasicGetAsync(
                queue:
                    queueName,

                autoAck:
                    true);

        return result is not null;
    }
}