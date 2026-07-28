using Xunit;

namespace DocumentProcessing.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class RabbitMqIntegrationCollection :
    ICollectionFixture<RabbitMqContainerFixture>
{
    public const string Name =
        "rabbitmq-integration";
}