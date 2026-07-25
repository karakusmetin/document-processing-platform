using DocumentProcessing.Contracts.Messaging;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.IntegrationTests.Infrastructure;
using DocumentProcessing.Messaging.RabbitMq.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Xunit;

namespace DocumentProcessing.IntegrationTests
    .Messaging.Reliability;

[Collection(
    RabbitMqIntegrationCollection.Name)]
public sealed class RabbitMqReliabilityTests
{
    private readonly RabbitMqContainerFixture _fixture;

    public RabbitMqReliabilityTests(
        RabbitMqContainerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture =
            fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Retry_WhenHandlerSchedulesDelayedRetry_MessageReturnsWithIncrementedAttempt()
    {
        RabbitMqReliabilityTestNames names =
            RabbitMqReliabilityTestNames.Create();

        ReliabilityMessageProbe probe =
            new(
                expectedMessageCount: 2);

        using IHost host =
            RabbitMqTestHostFactory
                .CreateReliabilityHost<
                    RetryOnceMessageHandler>(
                    _fixture.ConnectionString,
                    names,
                    probe);

        await host.StartAsync();

        IReadOnlyList<ReceivedReliabilityMessage>
            receivedMessages;

        try
        {
            IMessagePublisher publisher =
                host.Services.GetRequiredService<
                    IMessagePublisher>();

            string correlationId =
                Guid.NewGuid().ToString("N");

            ReliabilityTestRequested message =
                new()
                {
                    Id =
                        Guid.NewGuid(),

                    Value =
                        "delayed-retry-test"
                };

            await publisher.PublishAsync(
                message,
                new MessagePublishContext
                {
                    CorrelationId =
                        correlationId,

                    Attempt =
                        1
                },
                CancellationToken.None);

            receivedMessages =
                await probe.WaitAsync(
                    timeout:
                        TimeSpan.FromSeconds(10));
        }
        finally
        {
            await host.StopAsync();
        }

        ReceivedReliabilityMessage firstAttempt =
            Assert.Single(
                receivedMessages,
                static received =>
                    received.Envelope.Attempt == 1);

        ReceivedReliabilityMessage secondAttempt =
            Assert.Single(
                receivedMessages,
                static received =>
                    received.Envelope.Attempt == 2);

        Assert.Equal(
            firstAttempt.Envelope.Payload.Id,
            secondAttempt.Envelope.Payload.Id);

        Assert.Equal(
            firstAttempt.Envelope.Payload.Value,
            secondAttempt.Envelope.Payload.Value);

        /*
         * Retry yeni bir fiziksel mesajdır.
         */
        Assert.NotEqual(
            firstAttempt.Envelope.MessageId,
            secondAttempt.Envelope.MessageId);

        /*
         * Aynı iş akışı olduğu için correlation korunur.
         */
        Assert.Equal(
            firstAttempt.Envelope.CorrelationId,
            secondAttempt.Envelope.CorrelationId);

        /*
         * Retry mesajının sebebi ilk request mesajıdır.
         */
        Assert.Equal(
            firstAttempt.Envelope.MessageId.ToString("D"),
            secondAttempt.Envelope.CausationId);

        TimeSpan actualDelay =
            secondAttempt.ReceivedAtUtc -
            firstAttempt.ReceivedAtUtc;

        /*
         * TTL 1 saniye. Scheduling toleransı nedeniyle birebir
         * 1000 ms beklemiyoruz fakat anlık requeue olmadığını
         * doğruluyoruz.
         */
        Assert.True(
            actualDelay >=
            TimeSpan.FromMilliseconds(700),
            $"Expected delayed delivery, actual delay: " +
            $"{actualDelay}.");

        Assert.False(
            await RabbitMqBrokerTestClient
                .QueueContainsMessageAsync(
                    _fixture.ConnectionString,
                    names.RequestQueue));

        Assert.False(
            await RabbitMqBrokerTestClient
                .QueueContainsMessageAsync(
                    _fixture.ConnectionString,
                    names.RetryQueue));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DeadLetter_WhenHandlerRejects_MessageMovesToDeadLetterQueue()
    {
        RabbitMqReliabilityTestNames names =
            RabbitMqReliabilityTestNames.Create();

        ReliabilityMessageProbe probe =
            new(
                expectedMessageCount: 1);

        using IHost host =
            RabbitMqTestHostFactory
                .CreateReliabilityHost<
                    ForcedDeadLetterMessageHandler>(
                    _fixture.ConnectionString,
                    names,
                    probe);

        await host.StartAsync();

        IMessageSerializer serializer =
            host.Services.GetRequiredService<
                IMessageSerializer>();

        string correlationId =
            Guid.NewGuid().ToString("N");

        ReliabilityTestRequested message =
            new()
            {
                Id =
                    Guid.NewGuid(),

                Value =
                    "dead-letter-test"
            };

        try
        {
            IMessagePublisher publisher =
                host.Services.GetRequiredService<
                    IMessagePublisher>();

            await publisher.PublishAsync(
                message,
                new MessagePublishContext
                {
                    CorrelationId =
                        correlationId,

                    Attempt =
                        1
                },
                CancellationToken.None);

            await probe.WaitAsync(
                timeout:
                    TimeSpan.FromSeconds(5));
        }
        finally
        {
            /*
             * StopAsync in-flight callback'in tamamlanmasını,
             * dolayısıyla NACK işleminin bitmesini bekler.
             */
            await host.StopAsync();
        }

        BasicGetResult deadLetteredMessage =
            await RabbitMqBrokerTestClient
                .WaitForMessageAsync(
                    _fixture.ConnectionString,
                    names.DeadLetterQueue,
                    TimeSpan.FromSeconds(5));

        MessageEnvelope<ReliabilityTestRequested>
            deadLetteredEnvelope =
            serializer.Deserialize<
                ReliabilityTestRequested>(
                deadLetteredMessage.Body.ToArray());

        Assert.Equal(
            message.Id,
            deadLetteredEnvelope.Payload.Id);

        Assert.Equal(
            message.Value,
            deadLetteredEnvelope.Payload.Value);

        Assert.Equal(
            correlationId,
            deadLetteredEnvelope.CorrelationId);

        Assert.Equal(
            1,
            deadLetteredEnvelope.Attempt);

        Assert.Equal(
            names.DeadLetterExchange,
            deadLetteredMessage.Exchange);

        Assert.Equal(
            names.DeadLetterRoutingKey,
            deadLetteredMessage.RoutingKey);

        /*
         * Broker mesajı DLX üzerinden taşırken x-death
         * metadata'sı eklemelidir.
         */
        Assert.True(
            deadLetteredMessage
                .BasicProperties
                .Headers?
                .ContainsKey("x-death") ==
            true);

        Assert.False(
            await RabbitMqBrokerTestClient
                .QueueContainsMessageAsync(
                    _fixture.ConnectionString,
                    names.RequestQueue));
    }
}