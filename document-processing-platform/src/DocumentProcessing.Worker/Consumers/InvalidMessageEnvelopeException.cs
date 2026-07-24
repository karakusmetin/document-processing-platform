namespace DocumentProcessing.Worker.Consumers;

internal sealed class InvalidMessageEnvelopeException :
    Exception
{
    public InvalidMessageEnvelopeException(
        ConsumerFailureKind failureKind,
        string message)
        : base(message)
    {
        ValidateFailureKind(failureKind);

        FailureKind = failureKind;
    }

    public InvalidMessageEnvelopeException(
        ConsumerFailureKind failureKind,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        ValidateFailureKind(failureKind);

        FailureKind = failureKind;
    }

    public ConsumerFailureKind FailureKind { get; }

    private static void ValidateFailureKind(
        ConsumerFailureKind failureKind)
    {
        if (failureKind == ConsumerFailureKind.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "Envelope failure kind cannot be None.");
        }
    }
}