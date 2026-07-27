using System.Text.Json;
using System.Text.Json.Serialization;
using Queue.Messaging.Abstractions;

namespace Queue.Messaging.RabbitMq.Serialization;

internal sealed class SystemTextJsonMessageSerializer : IMessageSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public ReadOnlyMemory<byte> Serialize<TMessage>(
        MessageEnvelope<TMessage> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(
                envelope,
                SerializerOptions);
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException)
        {
            throw new MessageSerializationException(
                $"The message envelope for type " +
                $"'{typeof(TMessage).FullName}' could not be serialized.",
                exception);
        }
    }

    public MessageEnvelope<TMessage> Deserialize<TMessage>(
        ReadOnlyMemory<byte> body)
    {
        if (body.IsEmpty)
        {
            throw new ArgumentException(
                "Message body cannot be empty.",
                nameof(body));
        }

        try
        {
            MessageEnvelope<TMessage>? envelope =
                JsonSerializer.Deserialize<MessageEnvelope<TMessage>>(
                    body.Span,
                    SerializerOptions);

            return envelope
                ?? throw new MessageSerializationException(
                    $"The message envelope for type " +
                    $"'{typeof(TMessage).FullName}' was deserialized as null.",
                    new JsonException("Deserialized message was null."));
        }
        catch (MessageSerializationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException)
        {
            throw new MessageSerializationException(
                $"The message body could not be deserialized as an " +
                $"envelope containing '{typeof(TMessage).FullName}'.",
                exception);
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            DefaultIgnoreCondition =
                JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            NumberHandling = JsonNumberHandling.Strict,
            UnmappedMemberHandling =
                JsonUnmappedMemberHandling.Disallow
        };
    }
}