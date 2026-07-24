using DocumentProcessing.Contracts.Messaging;
using DocumentProcessing.Contracts.Messages;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Messaging.RabbitMq.Serialization;
using DocumentProcessing.Worker.Consumers.Retry;

namespace DocumentProcessing.Worker.Consumers;

internal sealed class ConversionRequestMessageHandler :
    IConversionRequestMessageHandler
{
    private readonly IMessageSerializer _messageSerializer;
    private readonly IConversionOrchestrator _orchestrator;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<ConversionRequestMessageHandler> _logger;
    private readonly IMessageRetryScheduler _retryScheduler;
    private readonly IRetryDelayProvider _retryDelayProvider;

    public ConversionRequestMessageHandler(
    IMessageSerializer messageSerializer,
    IConversionOrchestrator orchestrator,
    IMessagePublisher publisher,
    IMessageRetryScheduler retryScheduler,
    IRetryDelayProvider retryDelayProvider,
    ILogger<ConversionRequestMessageHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(messageSerializer);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(retryScheduler);
        ArgumentNullException.ThrowIfNull(retryDelayProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _messageSerializer = messageSerializer;
        _orchestrator = orchestrator;
        _publisher = publisher;
        _retryScheduler = retryScheduler;
        _retryDelayProvider = retryDelayProvider;
        _logger = logger;
    }

    public async Task<ConsumerMessageHandlingResult> HandleAsync(
     ConversionRequestDelivery delivery,
     CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        MessageEnvelope<ConversionRequested> envelope =
            _messageSerializer.Deserialize<ConversionRequested>(
                delivery.Body);

        ValidateEnvelope(envelope);

        ConversionRequested message =
            envelope.Payload;

        string correlationId =
            ResolveCorrelationId(
                envelope,
                message);

        using IDisposable? scope =
            _logger.BeginScope(
                new Dictionary<string, object>
                {
                    ["JobId"] = message.JobId,
                    ["MessageId"] = envelope.MessageId,
                    ["CorrelationId"] = correlationId,
                    ["Attempt"] = envelope.Attempt,
                    ["Redelivered"] = delivery.Redelivered
                });

        _logger.LogInformation(
            "Conversion request processing started. " +
            "SourceFileName: {SourceFileName}, " +
            "Profile: {Profile}",
            message.SourceFileName,
            message.Profile);

        /*
         * Conversion sonucunda yayınlanacak Completed veya Failed
         * eventinin mesaj bağlamıdır.
         *
         * CorrelationId aynı iş akışını korur.
         * CausationId ise bu eventi oluşturan request mesajını gösterir.
         */
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
             * Servis kapanışı nedeniyle işlem iptal edildiyse
             * retry üretmiyoruz. Exception üst katmana taşınır.
             *
             * Consumer channel kapanınca ACK edilmemiş request
             * RabbitMQ tarafından tekrar queue'ya alınır.
             */
            throw;
        }
        catch (Exception exception)
        {
            /*
             * Orchestrator normal bir result dönmek yerine
             * beklenmeyen exception attı.
             *
             * Bu durum da kontrollü delayed retry akışına alınır.
             */
            return await HandleUnexpectedProcessingFailureAsync(
                    envelope,
                    message,
                    correlationId,
                    resultPublishContext,
                    exception,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /*
         * Başarılı conversion.
         */
        if (result.IsSuccess)
        {
            ConversionCompleted completed =
                CreateCompletedMessage(
                    message,
                    correlationId,
                    result);

            /*
             * Completed eventi broker tarafından confirm edilmeden
             * request başarılı kabul edilmez.
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

            return ConsumerMessageHandlingResult.Acknowledge(
                "Conversion completed and result event was confirmed.");
        }

        /*
         * Provider hatayı retryable olarak sınıflandırdı.
         */
        if (result.Retryable)
        {
            /*
             * Mevcut attempt için tanımlı bir sonraki retry
             * süresi varsa yeni retry mesajı oluşturulur.
             */
            if (_retryDelayProvider.TryGetNextDelay(
                    envelope.Attempt,
                    out TimeSpan retryDelay))
            {
                /*
                 * Yeni retry envelope'u retry exchange'e yayınlanır.
                 *
                 * Bu metot publisher confirm alınmadan tamamlanmaz.
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
                 * Acknowledge conversion başarılı anlamına gelmiyor.
                 *
                 * Yeni retry mesajı RabbitMQ tarafından confirm
                 * edildiği için eski fiziksel request artık
                 * queue'dan silinebilir anlamına geliyor.
                 */
                return ConsumerMessageHandlingResult.Acknowledge(
                    $"Retry attempt {envelope.Attempt + 1} was " +
                    $"scheduled after {retryDelay}.");
            }

            /*
             * Hata retryable fakat kullanılabilecek retry süresi
             * kalmadı. MaximumAttempts tamamlandı.
             *
             * Artık terminal ConversionFailed eventi oluşturulur.
             */
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

            return ConsumerMessageHandlingResult.DeadLetter(
                ConsumerFailureKind.RetryAttemptsExhausted,
                "Maximum conversion retry attempts were exhausted.",
                diagnosticId);
        }

        /*
         * Provider Retryable=false döndürdü.
         *
         * Bu hata geçici değildir. Retry queue'ya gönderilmeden
         * doğrudan terminal ConversionFailed eventi oluşturulur.
         */
        string permanentFailureDiagnosticId =
            Guid.NewGuid().ToString("N");

        ConversionFailed failed =
            CreateFailedMessage(
                message,
                correlationId,
                envelope.Attempt,
                result,
                permanentFailureDiagnosticId);

        /*
         * Failed eventi broker tarafından confirm edildikten sonra
         * request mesajı DLQ'ya gönderilebilir.
         */
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

        return ConsumerMessageHandlingResult.DeadLetter(
            ConsumerFailureKind.PermanentConversionFailure,
            "Conversion provider returned a permanent failure.",
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

    private async Task<ConsumerMessageHandlingResult>
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

        /*
         * Exception oluştu ama hâlâ retry hakkı var.
         */
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

            /*
             * Aynı payload yeni MessageId ve artmış Attempt ile
             * retry exchange'e yayınlanır.
             */
            await _retryScheduler
                .ScheduleRetryAsync(
                    envelope,
                    retryDelay,
                    cancellationToken)
                .ConfigureAwait(false);

            return ConsumerMessageHandlingResult.Acknowledge(
                $"Unexpected processing failure retry attempt " +
                $"{envelope.Attempt + 1} was scheduled.");
        }

        /*
         * Beklenmeyen exception oluştu ve retry hakkı kalmadı.
         *
         * Bu durumda result nesnesi bulunmadığı için
         * CreateFailedMessage kullanamayız. ConversionFailed
         * mesajını burada doğrudan oluşturuyoruz.
         */
        ConversionFailed failed =
            new()
            {
                JobId =
                    message.JobId,

                CorrelationId =
                    correlationId,

                ErrorCode =
                    "UNEXPECTED_PROCESSING_ERROR",

                /*
                 * Exception.Message dış sisteme gönderilmiyor.
                 * Teknik detay yalnızca logda tutuluyor.
                 */
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

        return ConsumerMessageHandlingResult.DeadLetter(
            ConsumerFailureKind.RetryAttemptsExhausted,
            "Unexpected processing error exhausted all retry attempts.",
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

    private static string ResolveCorrelationId(
        MessageEnvelope<ConversionRequested> envelope,
        ConversionRequested message)
    {
        string? correlationId =
            !string.IsNullOrWhiteSpace(
                envelope.CorrelationId)
                ? envelope.CorrelationId
                : message.CorrelationId;

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new InvalidMessageEnvelopeException(
                ConsumerFailureKind.InvalidEnvelope,
                "ConversionRequested correlation ID is required.");
        }

        return correlationId;
    }

    private static void ValidateEnvelope(
    MessageEnvelope<ConversionRequested> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.MessageId == Guid.Empty)
        {
            throw new InvalidMessageEnvelopeException(
                ConsumerFailureKind.InvalidEnvelope,
                "MessageId cannot be empty.");
        }

        if (!string.Equals(
                envelope.MessageType,
                ConversionMessageTypes.ConversionRequested,
                StringComparison.Ordinal))
        {
            throw new InvalidMessageEnvelopeException(
                ConsumerFailureKind.UnsupportedMessageType,
                $"Unexpected RabbitMQ message type. " +
                $"Expected: " +
                $"'{ConversionMessageTypes.ConversionRequested}', " +
                $"Actual: '{envelope.MessageType}'.");
        }

        if (!string.Equals(
                envelope.MessageVersion,
                ConversionMessageVersions.V1,
                StringComparison.Ordinal))
        {
            throw new InvalidMessageEnvelopeException(
                ConsumerFailureKind.UnsupportedMessageVersion,
                $"Unsupported ConversionRequested message version. " +
                $"Expected: '{ConversionMessageVersions.V1}', " +
                $"Actual: '{envelope.MessageVersion}'.");
        }

        if (envelope.Attempt < 1)
        {
            throw new InvalidMessageEnvelopeException(
                ConsumerFailureKind.InvalidEnvelope,
                $"Message attempt must be greater than zero. " +
                $"Actual: {envelope.Attempt}.");
        }

        if (envelope.Payload is null)
        {
            throw new InvalidMessageEnvelopeException(
                ConsumerFailureKind.InvalidEnvelope,
                "ConversionRequested payload cannot be null.");
        }

        ValidatePayload(envelope.Payload);
    }

    private static void ValidatePayload(
    ConversionRequested message)
    {
        if (message.JobId == Guid.Empty)
        {
            throw new InvalidMessageEnvelopeException(
                ConsumerFailureKind.InvalidEnvelope,
                "ConversionRequested JobId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(
                message.SourceReference))
        {
            throw new InvalidMessageEnvelopeException(
                ConsumerFailureKind.InvalidEnvelope,
                "ConversionRequested SourceReference is required.");
        }

        if (string.IsNullOrWhiteSpace(
                message.SourceFileName))
        {
            throw new InvalidMessageEnvelopeException(
                ConsumerFailureKind.InvalidEnvelope,
                "ConversionRequested SourceFileName is required.");
        }

        if (string.IsNullOrWhiteSpace(
                message.Profile))
        {
            throw new InvalidMessageEnvelopeException(
                ConsumerFailureKind.InvalidEnvelope,
                "ConversionRequested Profile is required.");
        }
    }
}