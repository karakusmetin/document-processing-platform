namespace DocumentProcessing.Core.Errors;

public static class ConversionErrorCodes
{
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string SourceNotFound = "SOURCE_NOT_FOUND";
    public const string SourceAccessDenied = "SOURCE_ACCESS_DENIED";
    public const string UnsupportedFormat = "UNSUPPORTED_FORMAT";
    public const string SourceFileCorrupted = "SOURCE_FILE_CORRUPTED";
    public const string ProviderUnavailable = "PROVIDER_UNAVAILABLE";
    public const string ConversionTimeout = "CONVERSION_TIMEOUT";
    public const string ConversionOutputInvalid = "CONVERSION_OUTPUT_INVALID";
    public const string ArtifactWriteFailed = "ARTIFACT_WRITE_FAILED";
    public const string Unexpected = "UNEXPECTED_ERROR";
}
