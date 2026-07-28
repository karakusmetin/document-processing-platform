using Queue.Messaging.RabbitMq.Compatibility;

namespace Queue.Messaging.RabbitMq.Consuming;

/// <summary>
/// Bir mesaj handler'ının RabbitMQ consumer runtime'a döndürdüğü işlem sonucudur.
/// </summary>
public sealed record RabbitMqMessageHandlingResult(
    RabbitMqMessageDisposition Disposition,
    string Reason,
    string? FailureCode,
    string? DiagnosticId)
{
    public static RabbitMqMessageHandlingResult Acknowledge(
        string reason)
    {
        Guard.NotNullOrWhiteSpace(reason, nameof(reason));

        return new RabbitMqMessageHandlingResult(
            Disposition:
                RabbitMqMessageDisposition.Acknowledge,

            Reason:
                reason,

            FailureCode:
                null,

            DiagnosticId:
                null);
    }

    public static RabbitMqMessageHandlingResult DeadLetter(
        string failureCode,
        string reason,
        string diagnosticId)
    {
        Guard.NotNullOrWhiteSpace(
            failureCode, nameof(failureCode));

        Guard.NotNullOrWhiteSpace(
            reason, nameof(reason));

        Guard.NotNullOrWhiteSpace(
            diagnosticId, nameof(diagnosticId));

        return new RabbitMqMessageHandlingResult(
            Disposition:
                RabbitMqMessageDisposition.DeadLetter,

            Reason:
                reason,

            FailureCode:
                failureCode,

            DiagnosticId:
                diagnosticId);
    }
}