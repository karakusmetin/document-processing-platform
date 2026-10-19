using PdfSharp.Fonts;

namespace DocumentProcessing.Converters.Text.Rendering;

internal static class PdfFontSetup
{
    private static readonly Lock SyncRoot = new();

    public static void EnsureInitialized()
    {
        if (GlobalFontSettings.FontResolver is not null)
        {
            return;
        }

        lock (SyncRoot)
        {
            GlobalFontSettings.FontResolver ??= new InstalledFontResolver();
        }
    }
}
