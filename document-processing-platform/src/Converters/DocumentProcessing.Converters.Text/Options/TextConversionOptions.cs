namespace DocumentProcessing.Converters.Text.Options;

public sealed class TextConversionOptions
{
    public const string SectionName = "TextConversion";
    public string PageSize { get; init; } = "A4";
    public string FontFamily { get; init; } = "Consolas";
    public double FontSize { get; init; } = 10;
    public double MarginPoints { get; init; } = 40;
}
