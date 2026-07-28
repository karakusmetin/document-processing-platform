using DocumentProcessing.IntegrationTests.Infrastructure;
using RabbitMQ.Client;
using Xunit;

namespace DocumentProcessing.IntegrationTests.Messaging;

[Collection(
    RabbitMqIntegrationCollection.Name)]
public sealed class RabbitMqContainerSmokeTests
{
    private readonly RabbitMqContainerFixture _fixture;

    public RabbitMqContainerSmokeTests(
        RabbitMqContainerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Container_WhenStarted_AcceptsAmqpConnection()
    {
        ConnectionFactory connectionFactory =
            new()
            {
                Uri =
                    new Uri(
                        _fixture.ConnectionString)
            };

        await using IConnection connection =
            await connectionFactory.CreateConnectionAsync();

        Assert.True(connection.IsOpen);
    }
}