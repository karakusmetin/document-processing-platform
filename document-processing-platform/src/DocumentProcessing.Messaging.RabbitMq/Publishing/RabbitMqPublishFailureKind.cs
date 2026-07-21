namespace DocumentProcessing.Messaging.RabbitMq.Publishing;

public enum RabbitMqPublishFailureKind
{
    Unroutable = 1,
    BrokerRejected = 2,
    ConfirmationTimedOut = 3,
    TransportFailure = 4
}