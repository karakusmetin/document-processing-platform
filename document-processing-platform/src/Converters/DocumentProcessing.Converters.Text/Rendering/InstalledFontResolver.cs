using PdfSharp.Fonts;

namespace DocumentProcessing.Converters.Text.Rendering;

internal sealed class InstalledFontResolver : IFontResolver
{
    private static readonly string[] SansRegular = ["arial.ttf", "LiberationSans-Regular.ttf", "DejaVuSans.ttf"];
    private static readonly string[] SansBold = ["arialbd.ttf", "LiberationSans-Bold.ttf", "DejaVuSans-Bold.ttf"];
    private static readonly string[] SansItalic = ["ariali.ttf", "LiberationSans-Italic.ttf", "DejaVuSans-Oblique.ttf"];
    private static readonly string[] SansBoldItalic = ["arialbi.ttf", "LiberationSans-BoldItalic.ttf", "DejaVuSans-BoldOblique.ttf"];
    private static readonly string[] MonoRegular = ["consola.ttf", "cour.ttf", "LiberationMono-Regular.ttf", "DejaVuSansMono.ttf"];
    private static readonly string[] MonoBold = ["consolab.ttf", "courbd.ttf", "LiberationMono-Bold.ttf", "DejaVuSansMono-Bold.ttf"];
    private static readonly string[] MonoItalic = ["consolai.ttf", "couri.ttf", "LiberationMono-Italic.ttf", "DejaVuSansMono-Oblique.ttf"];
    private static readonly string[] MonoBoldItalic = ["consolaz.ttf", "courbi.ttf", "LiberationMono-BoldItalic.ttf", "DejaVuSansMono-BoldOblique.ttf"];
    private static readonly string[] SerifRegular = ["times.ttf", "LiberationSerif-Regular.ttf", "DejaVuSerif.ttf"];
    private static readonly string[] SerifBold = ["timesbd.ttf", "LiberationSerif-Bold.ttf", "DejaVuSerif-Bold.ttf"];
    private static readonly string[] SerifItalic = ["timesi.ttf", "LiberationSerif-Italic.ttf", "DejaVuSerif-Italic.ttf"];
    private static readonly string[] SerifBoldItalic = ["timesbi.ttf", "LiberationSerif-BoldItalic.ttf", "DejaVuSerif-BoldItalic.ttf"];

    private readonly IReadOnlyList<string> _fontDirectories = GetFontDirectories();

    public byte[] GetFont(string faceName) => File.ReadAllBytes(faceName);

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        string normalizedFamily = familyName.Trim().ToLowerInvariant();
        string[] candidates = normalizedFamily switch
        {
            "consolas" or "courier" or "courier new" => SelectStyle(
                isBold, isItalic, MonoRegular, MonoBold, MonoItalic, MonoBoldItalic),
            "times" or "times new roman" => SelectStyle(
                isBold, isItalic, SerifRegular, SerifBold, SerifItalic, SerifBoldItalic),
            _ => SelectStyle(
                isBold, isItalic, SansRegular, SansBold, SansItalic, SansBoldItalic)
        };

        string? fontPath = FindFont(candidates);
        if (fontPath is null)
        {
            throw new FileNotFoundException(
                $"No compatible font was found for '{familyName}'. Install Arial, Liberation, or DejaVu fonts.");
        }

        return new FontResolverInfo(fontPath);
    }

    private string? FindFont(IEnumerable<string> candidates)
    {
        foreach (string directory in _fontDirectories)
        {
            foreach (string candidate in candidates)
            {
                string directPath = Path.Combine(directory, candidate);
                if (File.Exists(directPath))
                {
                    return directPath;
                }

                try
                {
                    string? nestedPath = Directory
                        .EnumerateFiles(directory, candidate, SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (nestedPath is not null)
                    {
                        return nestedPath;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        return null;
    }

    private static string[] SelectStyle(
        bool isBold,
        bool isItalic,
        string[] regular,
        string[] bold,
        string[] italic,
        string[] boldItalic) =>
        (isBold, isItalic) switch
        {
            (true, true) => boldItalic,
            (true, false) => bold,
            (false, true) => italic,
            _ => regular
        };

    private static IReadOnlyList<string> GetFontDirectories()
    {
        List<string> candidates =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            Path.Combine(AppContext.BaseDirectory, "Fonts"),
            "/usr/share/fonts",
            "/usr/local/share/fonts",
            "/System/Library/Fonts",
            "/Library/Fonts"
        ];

        string runtimeRoot = Path.Combine(AppContext.BaseDirectory, "runtimes", "libreoffice");
        if (Directory.Exists(runtimeRoot))
        {
            candidates.AddRange(Directory
                .EnumerateDirectories(runtimeRoot)
                .Select(directory => Path.Combine(directory, "Fonts")));
        }

        return candidates
            .Where(directory => !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
