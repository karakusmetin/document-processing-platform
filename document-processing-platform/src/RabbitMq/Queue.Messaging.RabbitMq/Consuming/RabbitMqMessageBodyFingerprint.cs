using System.Security.Cryptography;

namespace Queue.Messaging.RabbitMq.Consuming;

internal static class RabbitMqMessageBodyFingerprint
{
    public static string ComputeSha256(
        ReadOnlySpan<byte> body)
    {
        byte[] hash =
            SHA256.HashData(body);

        return Convert
            .ToHexString(hash)
            .ToLowerInvariant();
    }
}