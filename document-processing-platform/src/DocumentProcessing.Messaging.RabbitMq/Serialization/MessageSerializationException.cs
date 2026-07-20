namespace DocumentProcessing.Messaging.RabbitMq.Serialization;

public sealed class MessageSerializationException(string message, Exception innerException) : Exception(message, innerException)
{
}