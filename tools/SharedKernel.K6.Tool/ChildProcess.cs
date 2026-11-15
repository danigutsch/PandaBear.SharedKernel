using System.Diagnostics;
using PandaBear.Processes;

namespace PandaBear.K6.Tool;

/// <summary>Runs child processes to their exit and stops their process trees.</summary>
internal static class ChildProcess
{
    /// <summary>How long a killed process tree may take to exit.</summary>
    public static readonly TimeSpan TerminationTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Runs a process to its exit and returns its exit code.</summary>
    /// <exception cref="InvalidOperationException">The process could not start.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled; the process tree is stopped first.</exception>
    public static async Task<int> Run(ProcessStartInfo startInfo, CancellationToken ct)
    {
        using var process = ProcessTree.Start(startInfo);
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await ProcessTree.Terminate(process, TerminationTimeout).ConfigureAwait(false);
            throw;
        }

        ct.ThrowIfCancellationRequested();
        return process.ExitCode;
    }
}
