namespace DocumentProcessing.Infrastructure.ProcessExecution;

public interface IExternalProcessRunner
{
    Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken);
}
