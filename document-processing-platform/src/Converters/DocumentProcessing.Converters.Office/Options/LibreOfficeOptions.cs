namespace DocumentProcessing.Converters.Office.Options;

public sealed class LibreOfficeOptions
{
    public const string SectionName = "LibreOffice";
    public string RuntimeRelativePath { get; init; } = string.Empty;
    public string ExecutableRelativePath { get; init; } = string.Empty;
    public string ProfileTemplateRelativePath { get; init; } = "profile-template.zip";
    public int TimeoutSeconds { get; init; } = 60;
    public string? WorkspaceRoot { get; init; }
}
