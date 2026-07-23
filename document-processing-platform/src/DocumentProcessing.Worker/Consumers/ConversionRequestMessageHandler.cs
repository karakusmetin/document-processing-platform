using DocumentProcessing.Contracts.Messaging;
using DocumentProcessing.Contracts.Messages;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Messaging.RabbitMq.Serialization;

namespace DocumentProcessing.Worker.Consumers;

internal sealed class ConversionRequestMessageHandler :
    IConversionRequestMessageHandler
{
    private readonly IMessageSerializer _messageSerializer;
    private readonly IConversionOrchestrator _orchestrator;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<ConversionRequestMessageHandler> _logger;

    public ConversionRequestMessageHandler(
        IMessageSerializer messageSerializer,
        IConversionOrchestrator orchestrator,
        IMessagePublisher publisher,
        ILogger<ConversionRequestMessageHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(messageSerializer);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(logger);

        _messageSerializer = messageSerializer;
        _orchestrator = orchestrator;
        _publisher = publisher;
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

        ConversionRequested message = envelope.Payload;

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

        ConversionExecutionResult result =
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

                        /*
                         * Attempt bilgisinin güvenilir kaynağı
                         * transport envelope'dur.
                         */
                        Attempt =
                            envelope.Attempt
                    },
                    cancellationToken)
                .ConfigureAwait(false);

        MessagePublishContext resultPublishContext =
            new()
            {
                CorrelationId =
                    correlationId,

                /*
                 * Oluşturulan result eventinin sebebi,
                 * gelen ConversionRequested mesajıdır.
                 */
                CausationId =
                    envelope.MessageId.ToString("D"),

                /*
                 * Bu, result eventinin kendi publish attempt'idir.
                 * Request'in attempt değeri değildir.
                 */
                Attempt = 1
            };

        if (result.IsSuccess)
        {
            ConversionCompleted completed =
                CreateCompletedMessage(
                    message,
                    correlationId,
                    result);

            /*
             * Completed event broker tarafından confirm edilmeden
             * handler başarılı dönmez.
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

        if (result.Retryable)
        {
            /*
             * Retryable failure için şimdilik Failed eventi
             * yayınlamıyoruz.
             *
             * ConversionFailed eventini terminal sonuç olarak
             * kullanacağız. Bir sonraki committe mesaj retry
             * exchange'e yayınlanacak.
             */
            _logger.LogWarning(
                "Conversion failed with a retryable error. " +
                "ErrorCode: {ErrorCode}, " +
                "FailedStage: {FailedStage}, " +
                "Attempt: {Attempt}",
                result.ErrorCode,
                result.FailedStage,
                envelope.Attempt);

            return ConsumerMessageHandlingResult.Requeue(
                "Conversion provider returned a retryable failure.");
        }

        ConversionFailed failed =
            CreateFailedMessage(
                message,
                correlationId,
                envelope.Attempt,
                result);

        /*
         * Kalıcı hata eventinin broker'a ulaştığı doğrulandıktan
         * sonra request dead-letter edilebilir.
         */
        await _publisher
            .PublishAsync(
                failed,
                resultPublishContext,
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogWarning(
            "Conversion failed permanently. " +
            "ErrorCode: {ErrorCode}, " +
            "FailedStage: {FailedStage}",
            result.ErrorCode,
            result.FailedStage);

        return ConsumerMessageHandlingResult.DeadLetter(
            "Conversion provider returned a non-retryable failure.");
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

    private static ConversionFailed CreateFailedMessage(
        ConversionRequested request,
        string correlationId,
        int attempt,
        ConversionExecutionResult result)
    {
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

            Retryable =
                false,

            FailedStage =
                result.FailedStage!,

            Attempt =
                attempt,

            DiagnosticId =
                Guid.NewGuid().ToString("N")
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
                "MessageId cannot be empty.");
        }

        if (!string.Equals(
                envelope.MessageType,
                ConversionMessageTypes.ConversionRequested,
                StringComparison.Ordinal))
        {
            throw new InvalidMessageEnvelopeException(
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
                $"Unsupported ConversionRequested message version. " +
                $"Expected: '{ConversionMessageVersions.V1}', " +
                $"Actual: '{envelope.MessageVersion}'.");
        }

        if (envelope.Attempt < 1)
        {
            throw new InvalidMessageEnvelopeException(
                $"Message attempt must be greater than zero. " +
                $"Actual: {envelope.Attempt}.");
        }

        if (envelope.Payload is null)
        {
            throw new InvalidMessageEnvelopeException(
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
                "ConversionRequested JobId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(
                message.SourceReference))
        {
            throw new InvalidMessageEnvelopeException(
                "ConversionRequested SourceReference is required.");
        }

        if (string.IsNullOrWhiteSpace(
                message.SourceFileName))
        {
            throw new InvalidMessageEnvelopeException(
                "ConversionRequested SourceFileName is required.");
        }

        if (string.IsNullOrWhiteSpace(
                message.Profile))
        {
            throw new InvalidMessageEnvelopeException(
                "ConversionRequested Profile is required.");
        }
    }
}