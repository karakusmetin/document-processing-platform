using Queue.Messaging.RabbitMq.Compatibility;

namespace Queue.Messaging.RabbitMq.Consuming;

internal sealed class RabbitMqMessageContractException :
    Exception
{
    public RabbitMqMessageContractException(
        string failureCode,
        string message)
        : base(message)
    {
        Guard.NotNullOrWhiteSpace(
            failureCode, nameof(failureCode));

        FailureCode = failureCode;
    }

    public RabbitMqMessageContractException(
        string failureCode,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Guard.NotNullOrWhiteSpace(
            failureCode, nameof(failureCode));

        FailureCode = failureCode;
    }

    public string FailureCode { get; }
}