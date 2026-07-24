namespace DocumentProcessing.Worker.Consumers;

internal sealed record ConsumerMessageHandlingResult(
    ConsumerMessageDisposition Disposition,
    string Reason,
    ConsumerFailureKind FailureKind,
    string? DiagnosticId)
{
    public static ConsumerMessageHandlingResult Acknowledge(
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new ConsumerMessageHandlingResult(
            ConsumerMessageDisposition.Acknowledge,
            reason,
            ConsumerFailureKind.None,
            DiagnosticId: null);
    }

    public static ConsumerMessageHandlingResult DeadLetter(
        ConsumerFailureKind failureKind,
        string reason,
        string diagnosticId)
    {
        if (failureKind == ConsumerFailureKind.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "Dead-letter failure kind cannot be None.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticId);

        return new ConsumerMessageHandlingResult(
            ConsumerMessageDisposition.DeadLetter,
            reason,
            failureKind,
            diagnosticId);
    }
}