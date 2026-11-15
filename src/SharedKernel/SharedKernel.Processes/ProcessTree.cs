using System.ComponentModel;
using System.Diagnostics;

namespace PandaBear.Processes;

/// <summary>
/// Starts a child process and ends its whole process tree, for hosts that keep a process running instead of running it
/// to completion through an <see cref="IProcessRunner"/>.
/// </summary>
public static class ProcessTree
{
    /// <summary>Starts the process that <paramref name="startInfo"/> describes.</summary>
    /// <param name="startInfo">The process to start.</param>
    /// <returns>The started process, which the caller disposes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="startInfo"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The process could not start. The message is <c>Failed to start &lt;file&gt;: &lt;reason&gt;</c> with the operating
    /// system's reason, which the inner exception also carries, or <c>Failed to start &lt;file&gt;.</c> without one.
    /// </exception>
    public static Process Start(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        try
        {
            return Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {startInfo.FileName}.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException($"Failed to start {startInfo.FileName}: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Kills the process tree unless the process has exited, then waits up to <paramref name="exitTimeout"/> for it to
    /// exit. Termination is best effort: a failure to kill or a tree that outlives the wait is ignored, so the failure or
    /// cancellation that started the cleanup is what reaches the caller.
    /// </summary>
    /// <param name="process">A process started by the caller.</param>
    /// <param name="exitTimeout">How long to wait for the tree to exit after the kill.</param>
    /// <returns>A task that completes when the tree has exited or the wait has ended.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exitTimeout"/> is negative.</exception>
    public static async Task Terminate(Process process, TimeSpan exitTimeout)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfLessThan(exitTimeout, TimeSpan.Zero);
        try
        {
            if (process.HasExited)
            {
                return;
            }

            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (IsCleanupFailure(exception))
        {
            // The process exited meanwhile, or part of the tree could not be killed: still wait for what was signalled.
        }

        try
        {
            using var termination = new CancellationTokenSource(exitTimeout);
            await process.WaitForExitAsync(termination.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsCleanupFailure(exception))
        {
            // Best-effort cleanup must not replace the caller's failure or cancellation.
        }
    }

    private static bool IsCleanupFailure(Exception exception) => exception is InvalidOperationException
        or Win32Exception
        or NotSupportedException
        or AggregateException
        or OperationCanceledException
        or IOException
        or ObjectDisposedException;
}
