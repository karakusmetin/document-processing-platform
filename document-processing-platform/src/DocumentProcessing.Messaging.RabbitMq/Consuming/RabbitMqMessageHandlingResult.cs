namespace DocumentProcessing.Messaging.RabbitMq.Consuming;

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
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(
            failureCode);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            reason);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            diagnosticId);

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