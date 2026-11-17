using System.Diagnostics;
using System.Text;

namespace DocumentProcessing.Infrastructure.ProcessExecution;

public sealed class ExternalProcessRunner : IExternalProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = request.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        StringBuilder stdErr = new();
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stdErr.AppendLine(e.Data); };

        process.Start();
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        process.BeginErrorReadLine();

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(request.Timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            await process.WaitForExitAsync(CancellationToken.None);
            await stdoutTask;

            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return new ProcessRunResult
            {
                ExitCode = -1,
                StandardError = stdErr.ToString(),
                TimedOut = true
            };
        }

        await stdoutTask;

        return new ProcessRunResult
        {
            ExitCode = process.ExitCode,
            StandardError = stdErr.ToString(),
            TimedOut = false
        };
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
