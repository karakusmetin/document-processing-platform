namespace DocumentProcessing.Messaging.RabbitMq.Consuming;

internal sealed class RabbitMqMessageContractException :
    Exception
{
    public RabbitMqMessageContractException(
        string failureCode,
        string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            failureCode);

        FailureCode = failureCode;
    }

    public RabbitMqMessageContractException(
        string failureCode,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            failureCode);

        FailureCode = failureCode;
    }

    public string FailureCode { get; }
}