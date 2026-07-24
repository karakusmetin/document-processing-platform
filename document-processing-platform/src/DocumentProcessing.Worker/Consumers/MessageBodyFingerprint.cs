using System.Security.Cryptography;

namespace DocumentProcessing.Worker.Consumers;

internal static class MessageBodyFingerprint
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