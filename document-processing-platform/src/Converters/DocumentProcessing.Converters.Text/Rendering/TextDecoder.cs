using System.Text;

namespace DocumentProcessing.Converters.Text.Rendering;

internal static class TextDecoder
{
    public static string Decode(byte[] content)
    {
        using MemoryStream stream = new(content);
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
