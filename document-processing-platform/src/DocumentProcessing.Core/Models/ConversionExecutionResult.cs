namespace DocumentProcessing.Core.Models;

public sealed record ConversionExecutionResult
{
    public ConversionExecutionResult() { }

    public bool IsSuccess { get; init; }
    public string? OutputReference { get; init; }
    public long OutputSize { get; init; }
    public string? OutputSha256 { get; init; }
    public int? PageCount { get; init; }
    public string? Provider { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool Retryable { get; init; }
    public string? FailedStage { get; init; }

    public static ConversionExecutionResult Success(
        string outputReference,
        long outputSize,
        string outputSha256,
        string provider,
        int? pageCount = null) => new()
    {
        IsSuccess = true,
        OutputReference = outputReference,
        OutputSize = outputSize,
        OutputSha256 = outputSha256,
        Provider = provider,
        PageCount = pageCount
    };

    public static ConversionExecutionResult Failure(
        string errorCode,
        string errorMessage,
        bool retryable,
        string failedStage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        Retryable = retryable,
        FailedStage = failedStage
    };
}
