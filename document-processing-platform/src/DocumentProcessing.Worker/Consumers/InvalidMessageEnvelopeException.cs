namespace DocumentProcessing.Worker.Consumers;

internal sealed class InvalidMessageEnvelopeException : Exception
{
    public InvalidMessageEnvelopeException(
        string message)
        : base(message)
    {
    }

    public InvalidMessageEnvelopeException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}