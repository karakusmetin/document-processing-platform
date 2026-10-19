using System.Text;
using DocumentProcessing.Converters.Text.Options;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace DocumentProcessing.Converters.Text.Rendering;

internal sealed class TextPdfRenderer(TextConversionOptions options)
{
    public byte[] Render(byte[] content)
    {
        PdfFontSetup.EnsureInitialized();

        string text = TextDecoder.Decode(content);
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        XFont font = new(options.FontFamily, options.FontSize, XFontStyleEx.Regular);

        using PdfDocument document = new();
        PdfPage page = PdfLayout.NewPage(document, options);
        XGraphics gfx = XGraphics.FromPdfPage(page);
        double lineHeight = font.GetHeight() * 1.15;
        double usableWidth = page.Width.Point - (2 * options.MarginPoints);
        double bottomLimit = page.Height.Point - options.MarginPoints;
        double y = options.MarginPoints;

        try
        {
            foreach (string rawLine in lines)
            {
                foreach (string wrapped in WrapLine(rawLine, font, gfx, usableWidth))
                {
                    if (y + lineHeight > bottomLimit)
                    {
                        gfx.Dispose();
                        page = PdfLayout.NewPage(document, options);
                        gfx = XGraphics.FromPdfPage(page);
                        y = options.MarginPoints;
                    }

                    gfx.DrawString(wrapped, font, XBrushes.Black, new XPoint(options.MarginPoints, y + font.Height));
                    y += lineHeight;
                }
            }
        }
        finally
        {
            gfx.Dispose();
        }

        using MemoryStream stream = new();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static IEnumerable<string> WrapLine(string line, XFont font, XGraphics gfx, double maxWidth)
    {
        if (line.Length == 0)
        {
            yield return string.Empty;
            yield break;
        }

        string[] words = line.Split(' ');
        StringBuilder current = new();

        foreach (string word in words)
        {
            string candidate = current.Length == 0 ? word : $"{current} {word}";
            if (current.Length > 0 && gfx.MeasureString(candidate, font).Width > maxWidth)
            {
                yield return current.ToString();
                current.Clear();
                current.Append(word);
            }
            else
            {
                current.Clear();
                current.Append(candidate);
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
