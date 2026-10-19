using DocumentProcessing.Converters.Text.Options;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace DocumentProcessing.Converters.Text.Rendering;

internal sealed class CsvPdfRenderer(TextConversionOptions options)
{
    public byte[] Render(byte[] content)
    {
        PdfFontSetup.EnsureInitialized();

        string text = TextDecoder.Decode(content);
        List<string[]> rows = CsvParser.Parse(text);

        using PdfDocument document = new();

        if (rows.Count == 0)
        {
            PdfLayout.NewPage(document, options);
            using MemoryStream emptyStream = new();
            document.Save(emptyStream, false);
            return emptyStream.ToArray();
        }

        XFont font = new(options.FontFamily, options.FontSize, XFontStyleEx.Regular);
        XFont headerFont = new(options.FontFamily, options.FontSize, XFontStyleEx.Bold);

        int columnCount = rows.Max(r => r.Length);
        PdfPage page = PdfLayout.NewPage(document, options);
        XGraphics gfx = XGraphics.FromPdfPage(page);
        double usableWidth = page.Width.Point - (2 * options.MarginPoints);
        double columnWidth = usableWidth / Math.Max(columnCount, 1);
        double rowHeight = font.GetHeight() * 1.4;
        double bottomLimit = page.Height.Point - options.MarginPoints;
        double y = options.MarginPoints;

        try
        {
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                if (y + rowHeight > bottomLimit)
                {
                    gfx.Dispose();
                    page = PdfLayout.NewPage(document, options);
                    gfx = XGraphics.FromPdfPage(page);
                    y = options.MarginPoints;
                }

                string[] row = rows[rowIndex];
                XFont rowFont = rowIndex == 0 ? headerFont : font;
                double x = options.MarginPoints;

                for (int col = 0; col < columnCount; col++)
                {
                    double horizontalPadding = Math.Min(3, columnWidth / 4);
                    double contentWidth = columnWidth - (2 * horizontalPadding);
                    string cell = col < row.Length ? row[col] : string.Empty;
                    gfx.DrawRectangle(XPens.LightGray, x, y, columnWidth, rowHeight);
                    gfx.DrawString(
                        Truncate(cell, rowFont, gfx, contentWidth),
                        rowFont,
                        XBrushes.Black,
                        new XRect(x + horizontalPadding, y, contentWidth, rowHeight),
                        XStringFormats.CenterLeft);
                    x += columnWidth;
                }

                y += rowHeight;
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

    private static string Truncate(string text, XFont font, XGraphics gfx, double maxWidth)
    {
        if (gfx.MeasureString(text, font).Width <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        if (gfx.MeasureString(ellipsis, font).Width > maxWidth)
        {
            return string.Empty;
        }

        while (text.Length > 0 && gfx.MeasureString(text + ellipsis, font).Width > maxWidth)
        {
            text = text[..^1];
        }

        return text + ellipsis;
    }
}
