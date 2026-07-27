using DocumentProcessing.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Xunit;
using Rabbit.Messaging.Abstractions;

namespace DocumentProcessing.IntegrationTests
    .Messaging.PublishConsume;

[Collection(
    RabbitMqIntegrationCollection.Name)]
public sealed class RabbitMqPublishConsumeTests
{
    private readonly RabbitMqContainerFixture _fixture;

    public RabbitMqPublishConsumeTests(
        RabbitMqContainerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PublishAsync_WhenRouteAndConsumerAreRegistered_MessageIsConsumedAndAcknowledged()
    {
        RabbitMqIntegrationTestNames names =
            RabbitMqIntegrationTestNames.Create();

        IntegrationTestMessageProbe probe =
            new();

        using IHost host =
            RabbitMqTestHostFactory
                .CreatePublishConsumeHost(
                    _fixture.ConnectionString,
                    names,
                    probe);

        await host
            .StartAsync();
        try
        {
            IMessagePublisher publisher =
                host.Services
                    .GetRequiredService<
                        IMessagePublisher>();

            Guid messageId =
                Guid.NewGuid();

            string correlationId =
                Guid.NewGuid().ToString("N");

            IntegrationTestRequested message =
                new()
                {
                    Id =
                        messageId,

                    Value =
                        "hello-from-integration-test"
                };

            MessagePublishContext publishContext =
                new()
                {
                    CorrelationId =
                        correlationId,

                    Attempt =
                        1
                };

            /*
             * Bu çağrı ortak RabbitMqPublisher üzerinden gerçek
             * broker'a publish eder ve publisher confirm
             * tamamlanmadan geri dönmez.
             */
            await publisher
                .PublishAsync(
                    message,
                    publishContext,
                    CancellationToken.None);

            ReceivedIntegrationTestMessage received =
                await probe
                    .WaitAsync(
                        timeout:
                            TimeSpan.FromSeconds(10),

                        cancellationToken:
                            CancellationToken.None);

            Assert.Equal(
                message.Id,
                received.Envelope.Payload.Id);

            Assert.Equal(
                message.Value,
                received.Envelope.Payload.Value);

            Assert.Equal(
                IntegrationTestMessageContracts
                    .RequestedMessageType,
                received.Envelope.MessageType);

            Assert.Equal(
                IntegrationTestMessageContracts.Version,
                received.Envelope.MessageVersion);

            Assert.Equal(
                correlationId,
                received.Envelope.CorrelationId);

            Assert.Equal(
                1,
                received.Envelope.Attempt);

            Assert.Equal(
                "document-processing-integration-tests",
                received.Envelope.Producer);

            Assert.Equal(
                names.ExchangeName,
                received.Delivery.Exchange);

            Assert.Equal(
                names.RoutingKey,
                received.Delivery.RoutingKey);

            Assert.False(
                received.Delivery.Redelivered);
        }
        finally
        {
            /*
             * Generic consumer hosted service:
             * - BasicCancel yapar
             * - in-flight mesajların bitmesini bekler
             * - channel'ı kapatır
             */
            await host
                .StopAsync();
        }

        /*
         * Handler ACK dönmemiş olsaydı channel kapanınca mesaj
         * yeniden queue'ya alınacaktı.
         *
         * Queue boşsa BasicAck broker'a ulaşmış demektir.
         */
        bool queueContainsMessage =
            await QueueContainsMessageAsync(
                    names.QueueName);

        Assert.False(
            queueContainsMessage);
    }

    private async Task<bool> QueueContainsMessageAsync(
        string queueName)
    {
        ConnectionFactory factory =
            new()
            {
                Uri =
                    new Uri(
                        _fixture.ConnectionString),

                ClientProvidedName =
                    "integration-test-queue-verifier"
            };

        await using IConnection connection =
            await factory
                .CreateConnectionAsync();

        await using IChannel channel =
            await connection
                .CreateChannelAsync();

        BasicGetResult? result =
            await channel
                .BasicGetAsync(
                    queue:
                        queueName,

                    autoAck:
                        true);

        return result is not null;
    }
}