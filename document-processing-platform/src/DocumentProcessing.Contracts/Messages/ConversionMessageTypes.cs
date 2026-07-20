namespace DocumentProcessing.Contracts.Messages;

public static class ConversionMessageTypes
{
    public const string ConversionRequested =
        "document-processing.conversion-requested";

    public const string ConversionCompleted =
        "document-processing.conversion-completed";

    public const string ConversionFailed =
        "document-processing.conversion-failed";
}