using System.Security.Cryptography;

namespace Queue.Messaging.RabbitMq.Compatibility;

internal static class HashCompatibility
{
    public static string ComputeSha256Hex(
        byte[] value)
    {
        Guard.NotNull(
            value,
            nameof(value));

#if NET10_0_OR_GREATER
        byte[] hash =
            SHA256.HashData(value);

        return Convert.ToHexString(
            hash);
#else
        using SHA256 sha256 =
            SHA256.Create();

        byte[] hash =
            sha256.ComputeHash(value);

        return BitConverter
            .ToString(hash)
            .Replace(
                "-",
                string.Empty);
#endif
    }
}