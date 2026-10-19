namespace DocumentProcessing.Core.Models;

public enum ConversionStage
{
    RequestValidation,
    SourceResolve,
    FormatDetection,
    ProviderSelection,
    Conversion,
    OutputValidation,
    ArtifactWrite
}

public static class ConversionStageExtensions
{
    public static string ToContractValue(this ConversionStage stage) => stage switch
    {
        ConversionStage.RequestValidation => "request-validation",
        ConversionStage.SourceResolve => "source-resolve",
        ConversionStage.FormatDetection => "format-detection",
        ConversionStage.ProviderSelection => "provider-selection",
        ConversionStage.Conversion => "conversion",
        ConversionStage.OutputValidation => "output-validation",
        ConversionStage.ArtifactWrite => "artifact-write",
        _ => "unknown"
    };
}
