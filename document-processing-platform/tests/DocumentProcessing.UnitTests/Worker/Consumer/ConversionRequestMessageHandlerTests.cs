using DocumentProcessing.Contracts.Messages;
using Rabbit.Messaging.Abstractions;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Messaging.RabbitMq.Consuming;
using DocumentProcessing.Worker.Consumers;
using DocumentProcessing.Worker.Consumers.Retry;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DocumentProcessing.UnitTests.Worker.Consumers;

public sealed class ConversionRequestMessageHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenConversionSucceeds_PublishesCompletedAndAcknowledges()
    {
        ConversionExecutionResult executionResult =
            CreateSuccessfulResult();

        StubConversionOrchestrator orchestrator =
            new(
                (_, _) =>
                    Task.FromResult(
                        executionResult));

        RecordingMessagePublisher publisher =
            new();

        RecordingMessageRetryScheduler retryScheduler =
            new();

        StubRetryDelayProvider retryDelayProvider =
            new();

        ConversionRequestMessageHandler handler =
            CreateHandler(
                orchestrator,
                publisher,
                retryScheduler,
                retryDelayProvider);

        MessageEnvelope<ConversionRequested> envelope =
            CreateEnvelope();

        RabbitMqMessageHandlingResult result =
            await handler.HandleAsync(
                envelope,
                CreateDeliveryContext(),
                CancellationToken.None);

        Assert.Equal(
            RabbitMqMessageDisposition.Acknowledge,
            result.Disposition);

        Assert.Null(result.FailureCode);
        Assert.Null(result.DiagnosticId);

        Assert.Single(
            orchestrator.Requests);

        ConversionRequest orchestratorRequest =
            orchestrator.Requests[0];

        Assert.Equal(
            envelope.Payload.JobId,
            orchestratorRequest.JobId);

        Assert.Equal(
            envelope.Attempt,
            orchestratorRequest.Attempt);

        PublishedMessage published =
            Assert.Single(
                publisher.Messages);

        ConversionCompleted completed =
            Assert.IsType<ConversionCompleted>(
                published.Message);

        Assert.Equal(
            envelope.Payload.JobId,
            completed.JobId);

        Assert.Equal(
            envelope.CorrelationId,
            completed.CorrelationId);

        Assert.Equal(
            executionResult.OutputReference,
            completed.OutputReference);

        Assert.Equal(
            executionResult.Provider,
            completed.Provider);

        Assert.Equal(
            envelope.CorrelationId,
            published.Context.CorrelationId);

        Assert.Equal(
            envelope.MessageId.ToString("D"),
            published.Context.CausationId);

        Assert.Equal(
            1,
            published.Context.Attempt);

        Assert.Empty(
            retryScheduler.Retries);
    }

    [Fact]
    public async Task HandleAsync_WhenFailureIsRetryable_SchedulesRetryAndAcknowledges()
    {
        StubConversionOrchestrator orchestrator =
            new(
                (_, _) =>
                    Task.FromResult(
                        CreateFailureResult(
                            retryable: true)));

        RecordingMessagePublisher publisher =
            new();

        RecordingMessageRetryScheduler retryScheduler =
            new();

        StubRetryDelayProvider retryDelayProvider =
            new(
                new Dictionary<int, TimeSpan>
                {
                    [1] =
                        TimeSpan.FromSeconds(10)
                });

        ConversionRequestMessageHandler handler =
            CreateHandler(
                orchestrator,
                publisher,
                retryScheduler,
                retryDelayProvider);

        MessageEnvelope<ConversionRequested> envelope =
            CreateEnvelope(
                attempt: 1);

        RabbitMqMessageHandlingResult result =
            await handler.HandleAsync(
                envelope,
                CreateDeliveryContext(),
                CancellationToken.None);

        Assert.Equal(
            RabbitMqMessageDisposition.Acknowledge,
            result.Disposition);

        ScheduledRetry retry =
            Assert.Single(
                retryScheduler.Retries);

        Assert.Equal(
            TimeSpan.FromSeconds(10),
            retry.Delay);

        MessageEnvelope<ConversionRequested> scheduledEnvelope =
            Assert.IsType<
                MessageEnvelope<ConversionRequested>>(
                retry.Envelope);

        Assert.Equal(
            envelope.MessageId,
            scheduledEnvelope.MessageId);

        /*
         * Retry scheduler yeni fiziksel envelope'u kendi içinde
         * oluşturur. Handler scheduler'a gelen mevcut envelope'u
         * gönderir.
         */
        Assert.Empty(
            publisher.Messages);
    }

    [Fact]
    public async Task HandleAsync_WhenRetryAttemptsAreExhausted_PublishesFailedAndDeadLetters()
    {
        StubConversionOrchestrator orchestrator =
            new(
                (_, _) =>
                    Task.FromResult(
                        CreateFailureResult(
                            retryable: true)));

        RecordingMessagePublisher publisher =
            new();

        RecordingMessageRetryScheduler retryScheduler =
            new();

        /*
         * Attempt 4 için tanımlı bir retry süresi yok.
         */
        StubRetryDelayProvider retryDelayProvider =
            new(
                delays:
                    new Dictionary<int, TimeSpan>(),

                maximumAttempts:
                    4);

        ConversionRequestMessageHandler handler =
            CreateHandler(
                orchestrator,
                publisher,
                retryScheduler,
                retryDelayProvider);

        MessageEnvelope<ConversionRequested> envelope =
            CreateEnvelope(
                attempt: 4);

        RabbitMqMessageHandlingResult result =
            await handler.HandleAsync(
                envelope,
                CreateDeliveryContext(),
                CancellationToken.None);

        Assert.Equal(
            RabbitMqMessageDisposition.DeadLetter,
            result.Disposition);

        Assert.Equal(
            ConversionFailureCodes
                .RetryAttemptsExhausted,
            result.FailureCode);

        Assert.False(
            string.IsNullOrWhiteSpace(
                result.DiagnosticId));

        Assert.Empty(
            retryScheduler.Retries);

        PublishedMessage published =
            Assert.Single(
                publisher.Messages);

        ConversionFailed failed =
            Assert.IsType<ConversionFailed>(
                published.Message);

        Assert.Equal(
            envelope.Payload.JobId,
            failed.JobId);

        Assert.Equal(
            envelope.Attempt,
            failed.Attempt);

        Assert.False(
            failed.Retryable);

        Assert.Equal(
            result.DiagnosticId,
            failed.DiagnosticId);
    }

    [Fact]
    public async Task HandleAsync_WhenFailureIsPermanent_PublishesFailedAndDeadLetters()
    {
        StubConversionOrchestrator orchestrator =
            new(
                (_, _) =>
                    Task.FromResult(
                        CreateFailureResult(
                            retryable: false)));

        RecordingMessagePublisher publisher =
            new();

        RecordingMessageRetryScheduler retryScheduler =
            new();

        StubRetryDelayProvider retryDelayProvider =
            new();

        ConversionRequestMessageHandler handler =
            CreateHandler(
                orchestrator,
                publisher,
                retryScheduler,
                retryDelayProvider);

        MessageEnvelope<ConversionRequested> envelope =
            CreateEnvelope();

        RabbitMqMessageHandlingResult result =
            await handler.HandleAsync(
                envelope,
                CreateDeliveryContext(),
                CancellationToken.None);

        Assert.Equal(
            RabbitMqMessageDisposition.DeadLetter,
            result.Disposition);

        Assert.Equal(
            ConversionFailureCodes.PermanentFailure,
            result.FailureCode);

        Assert.False(
            string.IsNullOrWhiteSpace(
                result.DiagnosticId));

        Assert.Empty(
            retryScheduler.Retries);

        PublishedMessage published =
            Assert.Single(
                publisher.Messages);

        ConversionFailed failed =
            Assert.IsType<ConversionFailed>(
                published.Message);

        Assert.False(
            failed.Retryable);

        Assert.Equal(
            "CONVERSION_FAILED",
            failed.ErrorCode);

        Assert.Equal(
            result.DiagnosticId,
            failed.DiagnosticId);
    }

    [Fact]
    public async Task HandleAsync_WhenRequestIsInvalid_DeadLettersWithoutCallingDependencies()
    {
        StubConversionOrchestrator orchestrator =
            new(
                (_, _) =>
                    throw new InvalidOperationException(
                        "Orchestrator must not be called."));

        RecordingMessagePublisher publisher =
            new();

        RecordingMessageRetryScheduler retryScheduler =
            new();

        StubRetryDelayProvider retryDelayProvider =
            new();

        ConversionRequestMessageHandler handler =
            CreateHandler(
                orchestrator,
                publisher,
                retryScheduler,
                retryDelayProvider);

        ConversionRequested invalidMessage =
            CreateMessage(
                jobId:
                    Guid.Empty);

        MessageEnvelope<ConversionRequested> envelope =
            CreateEnvelope(
                message:
                    invalidMessage);

        RabbitMqMessageHandlingResult result =
            await handler.HandleAsync(
                envelope,
                CreateDeliveryContext(),
                CancellationToken.None);

        Assert.Equal(
            RabbitMqMessageDisposition.DeadLetter,
            result.Disposition);

        Assert.Equal(
            ConversionFailureCodes.InvalidRequest,
            result.FailureCode);

        Assert.Empty(
            orchestrator.Requests);

        Assert.Empty(
            publisher.Messages);

        Assert.Empty(
            retryScheduler.Retries);
    }

    [Fact]
    public async Task HandleAsync_WhenOrchestratorThrowsAndRetryExists_SchedulesRetry()
    {
        StubConversionOrchestrator orchestrator =
            new(
                (_, _) =>
                    throw new InvalidOperationException(
                        "Test conversion failure."));

        RecordingMessagePublisher publisher =
            new();

        RecordingMessageRetryScheduler retryScheduler =
            new();

        StubRetryDelayProvider retryDelayProvider =
            new(
                new Dictionary<int, TimeSpan>
                {
                    [2] =
                        TimeSpan.FromSeconds(60)
                });

        ConversionRequestMessageHandler handler =
            CreateHandler(
                orchestrator,
                publisher,
                retryScheduler,
                retryDelayProvider);

        MessageEnvelope<ConversionRequested> envelope =
            CreateEnvelope(
                attempt: 2);

        RabbitMqMessageHandlingResult result =
            await handler.HandleAsync(
                envelope,
                CreateDeliveryContext(),
                CancellationToken.None);

        Assert.Equal(
            RabbitMqMessageDisposition.Acknowledge,
            result.Disposition);

        ScheduledRetry retry =
            Assert.Single(
                retryScheduler.Retries);

        Assert.Equal(
            TimeSpan.FromSeconds(60),
            retry.Delay);

        Assert.Empty(
            publisher.Messages);
    }

    [Fact]
    public async Task HandleAsync_WhenOrchestratorThrowsAndRetriesAreExhausted_PublishesFailed()
    {
        StubConversionOrchestrator orchestrator =
            new(
                (_, _) =>
                    throw new InvalidOperationException(
                        "Test conversion failure."));

        RecordingMessagePublisher publisher =
            new();

        RecordingMessageRetryScheduler retryScheduler =
            new();

        StubRetryDelayProvider retryDelayProvider =
            new(
                maximumAttempts:
                    4);

        ConversionRequestMessageHandler handler =
            CreateHandler(
                orchestrator,
                publisher,
                retryScheduler,
                retryDelayProvider);

        MessageEnvelope<ConversionRequested> envelope =
            CreateEnvelope(
                attempt: 4);

        RabbitMqMessageHandlingResult result =
            await handler.HandleAsync(
                envelope,
                CreateDeliveryContext(),
                CancellationToken.None);

        Assert.Equal(
            RabbitMqMessageDisposition.DeadLetter,
            result.Disposition);

        Assert.Equal(
            ConversionFailureCodes
                .UnexpectedFailureAttemptsExhausted,
            result.FailureCode);

        Assert.Empty(
            retryScheduler.Retries);

        PublishedMessage published =
            Assert.Single(
                publisher.Messages);

        ConversionFailed failed =
            Assert.IsType<ConversionFailed>(
                published.Message);

        Assert.Equal(
            "UNEXPECTED_PROCESSING_ERROR",
            failed.ErrorCode);

        Assert.False(
            failed.Retryable);

        Assert.Equal(
            envelope.Attempt,
            failed.Attempt);

        Assert.Equal(
            result.DiagnosticId,
            failed.DiagnosticId);
    }

    private static ConversionRequestMessageHandler
        CreateHandler(
            IConversionOrchestrator orchestrator,
            IMessagePublisher publisher,
            IMessageRetryScheduler retryScheduler,
            IRetryDelayProvider retryDelayProvider)
    {
        return new ConversionRequestMessageHandler(
            orchestrator,
            publisher,
            retryScheduler,
            retryDelayProvider,
            NullLogger<
                ConversionRequestMessageHandler>.Instance);
    }

    private static MessageEnvelope<ConversionRequested>
        CreateEnvelope(
            ConversionRequested? message = null,
            int attempt = 1)
    {
        ConversionRequested effectiveMessage =
            message ??
            CreateMessage();

        return MessageEnvelope<ConversionRequested>
            .Create(
                payload:
                    effectiveMessage,

                messageType:
                    ConversionMessageTypes
                        .ConversionRequested,

                messageVersion:
                    ConversionMessageVersions.V1,

                producer:
                    "document-processing-unit-tests",

                correlationId:
                    effectiveMessage.CorrelationId,

                causationId:
                    null,

                attempt:
                    attempt);
    }

    private static ConversionRequested CreateMessage(
        Guid? jobId = null)
    {
        return new ConversionRequested
        {
            JobId =
                jobId ??
                Guid.NewGuid(),

            CorrelationId =
                Guid.NewGuid().ToString("N"),

            SourceReference =
                "local://sample.docx",

            SourceFileName =
                "sample.docx",

            Profile =
                "display-copy"
        };
    }

    private static RabbitMqDeliveryContext
        CreateDeliveryContext()
    {
        return new RabbitMqDeliveryContext
        {
            Redelivered =
                false,

            Exchange =
                "document-processing.commands",

            RoutingKey =
                "document-processing.conversion-requested",

            BrokerMessageId =
                Guid.NewGuid().ToString("D"),

            BrokerCorrelationId =
                Guid.NewGuid().ToString("N"),

            BrokerMessageType =
                ConversionMessageTypes
                    .ConversionRequested
        };
    }

    private static ConversionExecutionResult
        CreateSuccessfulResult()
    {
        return new ConversionExecutionResult
        {
            IsSuccess =
                true,

            Retryable =
                false,

            OutputReference =
                "artifact://output/sample.pdf",

            OutputSize =
                12345,

            OutputSha256 =
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" +
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",

            PageCount =
                3,

            Provider =
                "unit-test-provider"
        };
    }

    private static ConversionExecutionResult
        CreateFailureResult(
            bool retryable)
    {
        return new ConversionExecutionResult
        {
            IsSuccess =
                false,

            Retryable =
                retryable,

            ErrorCode =
                "CONVERSION_FAILED",

            ErrorMessage =
                "The conversion provider returned an error.",

            FailedStage =
                "document-conversion"
        };
    }
}