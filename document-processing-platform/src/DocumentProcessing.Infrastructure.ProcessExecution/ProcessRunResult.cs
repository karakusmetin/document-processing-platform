namespace DocumentProcessing.Infrastructure.ProcessExecution;

public sealed class ProcessRunResult
{
    public required int ExitCode { get; init; }
    public required string StandardError { get; init; }
    public required bool TimedOut { get; init; }
}
