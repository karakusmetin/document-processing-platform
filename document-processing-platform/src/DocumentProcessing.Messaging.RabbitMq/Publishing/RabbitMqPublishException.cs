namespace DocumentProcessing.Messaging.RabbitMq.Publishing;

public sealed class RabbitMqPublishException : Exception
{
    public RabbitMqPublishException(
        RabbitMqPublishFailureKind failureKind,
        Guid messageId,
        string exchange,
        string routingKey,
        bool outcomeUnknown,
        Exception innerException)
        : base(
            CreateMessage(
                failureKind,
                messageId,
                exchange,
                routingKey,
                outcomeUnknown),
            innerException)
    {
        FailureKind = failureKind;
        MessageId = messageId;
        Exchange = exchange;
        RoutingKey = routingKey;
        OutcomeUnknown = outcomeUnknown;
    }

    public RabbitMqPublishFailureKind FailureKind { get; }

    public Guid MessageId { get; }

    public string Exchange { get; }

    public string RoutingKey { get; }

    public bool OutcomeUnknown { get; }

    private static string CreateMessage(
        RabbitMqPublishFailureKind failureKind,
        Guid messageId,
        string exchange,
        string routingKey,
        bool outcomeUnknown)
    {
        return
            $"RabbitMQ publish failed. " +
            $"FailureKind: {failureKind}, " +
            $"MessageId: {messageId:D}, " +
            $"Exchange: {exchange}, " +
            $"RoutingKey: {routingKey}, " +
            $"OutcomeUnknown: {outcomeUnknown}.";
    }
}