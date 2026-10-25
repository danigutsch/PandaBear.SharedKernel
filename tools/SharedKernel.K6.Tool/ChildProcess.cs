using System.ComponentModel;
using System.Diagnostics;

namespace PandaBear.K6.Tool;

/// <summary>Starts child processes and stops their process trees.</summary>
internal static class ChildProcess
{
    private static readonly TimeSpan TerminationTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The variables that hold a GitHub token, which no child process receives.</summary>
    private static readonly string[] TokenVariables = ["GH_TOKEN", "GITHUB_TOKEN"];

    /// <summary>Starts a process.</summary>
    /// <exception cref="InvalidOperationException">The process could not start.</exception>
    public static Process Start(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {startInfo.FileName}.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException($"Failed to start {startInfo.FileName}: {exception.Message}", exception);
        }
    }

    /// <summary>Runs a process to its exit and returns its exit code.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled; the process tree is stopped first.</exception>
    public static async Task<int> Run(ProcessStartInfo startInfo, CancellationToken ct)
    {
        using var process = Start(startInfo);
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await Terminate(process).ConfigureAwait(false);
            throw;
        }

        ct.ThrowIfCancellationRequested();
        return process.ExitCode;
    }

    /// <summary>Kills the process tree unless the process has exited, and waits up to five seconds for it to exit.</summary>
    /// <remarks>Termination failures are ignored, so the failure or cancellation that started cleanup reaches the caller.</remarks>
    public static async Task Terminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var termination = new CancellationTokenSource(TerminationTimeout);
                await process.WaitForExitAsync(termination.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or Win32Exception
            or NotSupportedException
            or AggregateException
            or OperationCanceledException)
        {
            // Best-effort cleanup must not replace the caller's failure or cancellation.
        }
    }

    /// <summary>Removes the GitHub token variables, in any case, from the child environment.</summary>
    public static void RemoveTokens(ProcessStartInfo startInfo)
    {
        foreach (var name in startInfo.Environment.Keys.Where(static name => TokenVariables.Contains(name, StringComparer.OrdinalIgnoreCase)).ToArray())
        {
            startInfo.Environment.Remove(name);
        }
    }
}
