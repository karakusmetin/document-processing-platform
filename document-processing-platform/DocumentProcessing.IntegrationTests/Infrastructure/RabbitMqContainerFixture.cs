using Testcontainers.RabbitMq;
using Xunit;

namespace DocumentProcessing.IntegrationTests.Infrastructure;

public sealed class RabbitMqContainerFixture :
    IAsyncLifetime
{
    private readonly RabbitMqContainer _container =
        new RabbitMqBuilder(
            "rabbitmq:4.3.4-management")
        .Build();

    public string ConnectionString =>
        _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}