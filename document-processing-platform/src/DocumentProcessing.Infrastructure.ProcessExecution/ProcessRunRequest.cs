namespace DocumentProcessing.Infrastructure.ProcessExecution;

public sealed class ProcessRunRequest
{
    public required string FileName { get; init; }
    public required IReadOnlyList<string> Arguments { get; init; }
    public required TimeSpan Timeout { get; init; }
}
