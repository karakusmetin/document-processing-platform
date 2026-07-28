namespace Queue.Messaging.RabbitMq.Consuming;

/// <summary>
/// Consumer runtime tarafından üretilebilen transport ve mesaj sözleşmesi kaynaklı standart hata kodlarıdır.
/// </summary>
public static class RabbitMqConsumerFailureCodes
{
    public const string MalformedMessage =
        "rabbitmq.malformed-message";

    public const string InvalidEnvelope =
        "rabbitmq.invalid-envelope";

    public const string UnsupportedMessageType =
        "rabbitmq.unsupported-message-type";

    public const string UnsupportedMessageVersion =
        "rabbitmq.unsupported-message-version";

    public const string HandlerRejected =
        "rabbitmq.handler-rejected";
}