using Queue.Messaging.RabbitMq.Compatibility;
using System.Security.Cryptography;

namespace Queue.Messaging.RabbitMq.Consuming;

internal static class RabbitMqMessageBodyFingerprint
{
    public static string ComputeSha256(ReadOnlySpan<byte> body)
    {
        return HashCompatibility.ComputeSha256Hex(body.ToArray());
    }
}