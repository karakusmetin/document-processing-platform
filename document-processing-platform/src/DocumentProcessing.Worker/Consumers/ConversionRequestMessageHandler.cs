using DocumentProcessing.Contracts.Messages;
using Rabbit.Messaging.Abstractions;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Messaging.RabbitMq.Consuming;
using DocumentProcessing.Worker.Consumers.Retry;

namespace DocumentProcessing.Worker.Consumers;

internal sealed class ConversionRequestMessageHandler : IRabbitMqMessageHandler<ConversionRequested>
{
    private readonly IConversionOrchestrator _orchestrator;
    private readonly IMessagePublisher _publisher;
    private readonly IMessageRetryScheduler _retryScheduler;
    private readonly IRetryDelayProvider _retryDelayProvider;
    private readonly ILogger<ConversionRequestMessageHandler> _logger;

    public ConversionRequestMessageHandler(
    IConversionOrchestrator orchestrator,
    IMessagePublisher publisher,
    IMessageRetryScheduler retryScheduler,
    IRetryDelayProvider retryDelayProvider,
    ILogger<ConversionRequestMessageHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(retryScheduler);
        ArgumentNullException.ThrowIfNull(retryDelayProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _orchestrator = orchestrator;
        _publisher = publisher;
        _retryScheduler = retryScheduler;
        _retryDelayProvider = retryDelayProvider;
        _logger = logger;
    }

    public async Task<RabbitMqMessageHandlingResult> HandleAsync(
        MessageEnvelope<ConversionRequested> envelope,
        RabbitMqDeliveryContext delivery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(delivery);

        ConversionRequested message =
            envelope.Payload;

        if (!TryValidateRequest(
                envelope,
                message,
                out string correlationId,
                out string validationFailureReason))
        {
            string diagnosticId =
                Guid.NewGuid().ToString("N");

            _logger.LogError(
                "Conversion request validation failed. " +
                "DiagnosticId: {DiagnosticId}, " +
                "Reason: {Reason}, " +
                "MessageId: {MessageId}, " +
                "Exchange: {Exchange}, " +
                "RoutingKey: {RoutingKey}",
                diagnosticId,
                validationFailureReason,
                envelope.MessageId,
                delivery.Exchange,
                delivery.RoutingKey);

            return RabbitMqMessageHandlingResult.DeadLetter(
                failureCode:
                    ConversionFailureCodes.InvalidRequest,

                reason:
                    validationFailureReason,

                diagnosticId:
                    diagnosticId);
        }

        using IDisposable? scope =
            _logger.BeginScope(
                new Dictionary<string, object>
                {
                    ["JobId"] =
                        message.JobId,

                    ["MessageId"] =
                        envelope.MessageId,

                    ["CorrelationId"] =
                        correlationId,

                    ["Attempt"] =
                        envelope.Attempt,

                    ["Redelivered"] =
                        delivery.Redelivered
                });

        _logger.LogInformation(
            "Conversion request processing started. " +
            "SourceFileName: {SourceFileName}, " +
            "Profile: {Profile}",
            message.SourceFileName,
            message.Profile);

        MessagePublishContext resultPublishContext =
            new()
            {
                CorrelationId =
                    correlationId,

                CausationId =
                    envelope.MessageId.ToString("D"),

                Attempt = 1
            };

        ConversionExecutionResult result;

        try
        {
            result =
                await _orchestrator
                    .ExecuteAsync(
                        new ConversionRequest
                        {
                            JobId =
                                message.JobId,

                            CorrelationId =
                                correlationId,

                            SourceReference =
                                message.SourceReference,

                            SourceFileName =
                                message.SourceFileName,

                            Profile =
                                message.Profile,

                            Attempt =
                                envelope.Attempt
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            /*
             * Servis kapanışı sırasında işlem iptal edilmişse
             * generic runtime'a geri taşınır.
             *
             * Runtime mesajı ACK/NACK etmez. Channel kapandığında
             * unacked mesaj yeniden queue'ya döner.
             */
            throw;
        }
        catch (Exception exception)
        {
            return await HandleUnexpectedProcessingFailureAsync(
                    envelope,
                    message,
                    correlationId,
                    resultPublishContext,
                    exception,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (result.IsSuccess)
        {
            ConversionCompleted completed =
                CreateCompletedMessage(
                    message,
                    correlationId,
                    result);

            /*
             * Event publisher confirm almadan bu çağrı tamamlanmaz.
             */
            await _publisher
                .PublishAsync(
                    completed,
                    resultPublishContext,
                    cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Conversion completed. " +
                "Provider: {Provider}, " +
                "OutputReference: {OutputReference}",
                result.Provider,
                result.OutputReference);

            /*
             * Generic runtime bu sonucu alınca request mesajını ACK eder.
             */
            return RabbitMqMessageHandlingResult.Acknowledge(
                "Conversion completed and result event was confirmed.");
        }

        if (result.Retryable)
        {
            if (_retryDelayProvider.TryGetNextDelay(
                    envelope.Attempt,
                    out TimeSpan retryDelay))
            {
                /*
                 * Yeni retry envelope'u yayınlanır ve broker confirm
                 * alındıktan sonra metot tamamlanır.
                 */
                await _retryScheduler
                    .ScheduleRetryAsync(
                        envelope,
                        retryDelay,
                        cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogWarning(
                    "Conversion failed with a retryable error. " +
                    "A delayed retry was scheduled. " +
                    "ErrorCode: {ErrorCode}, " +
                    "FailedStage: {FailedStage}, " +
                    "CurrentAttempt: {CurrentAttempt}, " +
                    "NextAttempt: {NextAttempt}, " +
                    "RetryDelay: {RetryDelay}",
                    result.ErrorCode,
                    result.FailedStage,
                    envelope.Attempt,
                    envelope.Attempt + 1,
                    retryDelay);

                /*
                 * Generic runtime eski fiziksel request mesajını ACK eder.
                 *
                 * Bu ACK conversion başarılı demek değildir.
                 * Retry mesajının güvenle broker'a taşındığı anlamına gelir.
                 */
                return RabbitMqMessageHandlingResult.Acknowledge(
                    $"Retry attempt {envelope.Attempt + 1} was " +
                    $"scheduled after {retryDelay}.");
            }

            string diagnosticId =
                Guid.NewGuid().ToString("N");

            ConversionFailed exhaustedFailure =
                CreateFailedMessage(
                    message,
                    correlationId,
                    envelope.Attempt,
                    result,
                    diagnosticId);

            await _publisher
                .PublishAsync(
                    exhaustedFailure,
                    resultPublishContext,
                    cancellationToken)
                .ConfigureAwait(false);

            _logger.LogError(
                "Conversion retry attempts were exhausted. " +
                "DiagnosticId: {DiagnosticId}, " +
                "ErrorCode: {ErrorCode}, " +
                "FailedStage: {FailedStage}, " +
                "Attempt: {Attempt}, " +
                "MaximumAttempts: {MaximumAttempts}",
                diagnosticId,
                result.ErrorCode,
                result.FailedStage,
                envelope.Attempt,
                _retryDelayProvider.MaximumAttempts);

            /*
             * Generic runtime NACK/requeue:false uygulayacak.
             */
            return RabbitMqMessageHandlingResult.DeadLetter(
                failureCode:
                    ConversionFailureCodes.RetryAttemptsExhausted,

                reason:
                    "Maximum conversion retry attempts were exhausted.",

                diagnosticId:
                    diagnosticId);
        }

        string permanentFailureDiagnosticId =
            Guid.NewGuid().ToString("N");

        ConversionFailed failed =
            CreateFailedMessage(
                message,
                correlationId,
                envelope.Attempt,
                result,
                permanentFailureDiagnosticId);

        await _publisher
            .PublishAsync(
                failed,
                resultPublishContext,
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogError(
            "Conversion failed permanently. " +
            "DiagnosticId: {DiagnosticId}, " +
            "ErrorCode: {ErrorCode}, " +
            "FailedStage: {FailedStage}, " +
            "Attempt: {Attempt}",
            permanentFailureDiagnosticId,
            result.ErrorCode,
            result.FailedStage,
            envelope.Attempt);

        return RabbitMqMessageHandlingResult.DeadLetter(
            failureCode:
                ConversionFailureCodes.PermanentFailure,

            reason:
                "Conversion provider returned a permanent failure.",

            diagnosticId:
                permanentFailureDiagnosticId);
    }

    private static ConversionCompleted CreateCompletedMessage(
        ConversionRequested request,
        string correlationId,
        ConversionExecutionResult result)
    {
        return new ConversionCompleted
        {
            JobId =
                request.JobId,

            CorrelationId =
                correlationId,

            OutputReference =
                result.OutputReference!,

            OutputFormat =
                "pdf",

            OutputSize =
                result.OutputSize,

            OutputSha256 =
                result.OutputSha256!,

            PageCount =
                result.PageCount,

            Provider =
                result.Provider!
        };
    }

    private async Task<RabbitMqMessageHandlingResult>
    HandleUnexpectedProcessingFailureAsync(
        MessageEnvelope<ConversionRequested> envelope,
        ConversionRequested message,
        string correlationId,
        MessagePublishContext publishContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        string diagnosticId =
            Guid.NewGuid().ToString("N");

        if (_retryDelayProvider.TryGetNextDelay(
                envelope.Attempt,
                out TimeSpan retryDelay))
        {
            _logger.LogError(
                exception,
                "Unexpected error occurred during conversion processing. " +
                "A delayed retry will be scheduled. " +
                "DiagnosticId: {DiagnosticId}, " +
                "CurrentAttempt: {CurrentAttempt}, " +
                "NextAttempt: {NextAttempt}, " +
                "RetryDelay: {RetryDelay}",
                diagnosticId,
                envelope.Attempt,
                envelope.Attempt + 1,
                retryDelay);

            await _retryScheduler
                .ScheduleRetryAsync(
                    envelope,
                    retryDelay,
                    cancellationToken)
                .ConfigureAwait(false);

            return RabbitMqMessageHandlingResult.Acknowledge(
                $"Unexpected processing failure retry attempt " +
                $"{envelope.Attempt + 1} was scheduled.");
        }

        ConversionFailed failed =
            new()
            {
                JobId =
                    message.JobId,

                CorrelationId =
                    correlationId,

                ErrorCode =
                    "UNEXPECTED_PROCESSING_ERROR",

                Message =
                    "An unexpected error occurred while processing " +
                    "the conversion request.",

                Retryable =
                    false,

                FailedStage =
                    "conversion-processing",

                Attempt =
                    envelope.Attempt,

                DiagnosticId =
                    diagnosticId
            };

        await _publisher
            .PublishAsync(
                failed,
                publishContext,
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogError(
            exception,
            "Unexpected conversion processing error became terminal. " +
            "DiagnosticId: {DiagnosticId}, " +
            "Attempt: {Attempt}, " +
            "MaximumAttempts: {MaximumAttempts}",
            diagnosticId,
            envelope.Attempt,
            _retryDelayProvider.MaximumAttempts);

        return RabbitMqMessageHandlingResult.DeadLetter(
            failureCode:
                ConversionFailureCodes
                    .UnexpectedFailureAttemptsExhausted,

            reason:
                "Unexpected processing error exhausted all retry attempts.",

            diagnosticId:
                diagnosticId);
    }

    private static ConversionFailed CreateFailedMessage(
    ConversionRequested request,
    string correlationId,
    int attempt,
    ConversionExecutionResult result,
    string diagnosticId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            diagnosticId);

        return new ConversionFailed
        {
            JobId =
                request.JobId,

            CorrelationId =
                correlationId,

            ErrorCode =
                result.ErrorCode!,

            Message =
                result.ErrorMessage!,

            /*
             * Bu mesaj sadece terminal aşamada oluşturuluyor.
             *
             * İlk hata retryable olsa bile buraya gelindiyse
             * artık başka otomatik retry yapılmayacak.
             */
            Retryable =
                false,

            FailedStage =
                result.FailedStage!,

            Attempt =
                attempt,

            DiagnosticId =
                diagnosticId
        };
    }

    private static bool TryValidateRequest(
    MessageEnvelope<ConversionRequested> envelope,
    ConversionRequested message,
    out string correlationId,
    out string failureReason)
    {
        correlationId = string.Empty;
        failureReason = string.Empty;

        if (message.JobId == Guid.Empty)
        {
            failureReason =
                "ConversionRequested JobId cannot be empty.";

            return false;
        }

        string? envelopeCorrelationId =
            envelope.CorrelationId;

        string? payloadCorrelationId =
            message.CorrelationId;

        if (!string.IsNullOrWhiteSpace(envelopeCorrelationId) &&
            !string.IsNullOrWhiteSpace(payloadCorrelationId) &&
            !string.Equals(
                envelopeCorrelationId,
                payloadCorrelationId,
                StringComparison.Ordinal))
        {
            failureReason =
                "Envelope and payload correlation IDs do not match.";

            return false;
        }

        correlationId =
            !string.IsNullOrWhiteSpace(envelopeCorrelationId)
                ? envelopeCorrelationId
                : payloadCorrelationId;

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            failureReason =
                "ConversionRequested correlation ID is required.";

            return false;
        }

        if (string.IsNullOrWhiteSpace(
                message.SourceReference))
        {
            failureReason =
                "ConversionRequested SourceReference is required.";

            return false;
        }

        if (string.IsNullOrWhiteSpace(
                message.SourceFileName))
        {
            failureReason =
                "ConversionRequested SourceFileName is required.";

            return false;
        }

        if (string.IsNullOrWhiteSpace(
                message.Profile))
        {
            failureReason =
                "ConversionRequested Profile is required.";

            return false;
        }

        return true;
    }
}