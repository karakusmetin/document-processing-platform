using DocumentProcessing.Converters.Text.Options;
using PdfSharp.Pdf;

namespace DocumentProcessing.Converters.Text.Rendering;

internal static class PdfLayout
{
    public static PdfPage NewPage(PdfDocument document, TextConversionOptions options)
    {
        PdfPage page = document.AddPage();
        page.Size = ResolvePageSize(options.PageSize);
        return page;
    }

    private static PdfSharp.PageSize ResolvePageSize(string pageSize) => pageSize.ToUpperInvariant() switch
    {
        "LETTER" => PdfSharp.PageSize.Letter,
        "A4" => PdfSharp.PageSize.A4,
        _ => PdfSharp.PageSize.A4
    };
}
