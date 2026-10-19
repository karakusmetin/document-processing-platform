using DocumentProcessing.Contracts.Converter;

namespace DocumentProcessing.Core.Models;

public sealed record FileFormat(string Name, string MediaType, string DefaultExtension)
{
    public static readonly FileFormat Pdf = new("pdf", "application/pdf", ".pdf");
    public static readonly FileFormat Text = new("txt", "text/plain", ".txt");
    public static readonly FileFormat Csv = new("csv", "text/csv", ".csv");
    public static readonly FileFormat Unknown = new("unknown", "application/octet-stream", string.Empty);

    public static IReadOnlyDictionary<DocumentType, FileFormat> OfficeFormats { get; } =
        new Dictionary<DocumentType, FileFormat>
        {
            [DocumentType.Doc] = new("doc", "application/msword", ".doc"),
            [DocumentType.Docx] = new("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx"),
            [DocumentType.Rtf] = new("rtf", "application/rtf", ".rtf"),
            [DocumentType.Xls] = new("xls", "application/vnd.ms-excel", ".xls"),
            [DocumentType.Xlsx] = new("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx"),
            [DocumentType.Ppt] = new("ppt", "application/vnd.ms-powerpoint", ".ppt"),
            [DocumentType.Pptx] = new("pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation", ".pptx")
        }.AsReadOnly();

    public static IReadOnlyDictionary<DocumentType, FileFormat> TextFormats { get; } =
        new Dictionary<DocumentType, FileFormat>
        {
            [DocumentType.Txt] = Text,
            [DocumentType.Csv] = Csv
        }.AsReadOnly();

    public static IReadOnlyDictionary<DocumentType, FileFormat> ConvertibleFormats { get; } =
        OfficeFormats
            .Concat(TextFormats)
            .ToDictionary(pair => pair.Key, pair => pair.Value)
            .AsReadOnly();
}
