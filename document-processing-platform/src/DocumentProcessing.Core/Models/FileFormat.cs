namespace DocumentProcessing.Core.Models;

public sealed record FileFormat(string Name, string MediaType, string DefaultExtension)
{
    public static readonly FileFormat Pdf = new("pdf", "application/pdf", ".pdf");
    public static readonly FileFormat Unknown = new("unknown", "application/octet-stream", string.Empty);
}
