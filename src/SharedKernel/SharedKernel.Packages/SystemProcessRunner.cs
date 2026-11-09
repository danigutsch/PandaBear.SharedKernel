using System.ComponentModel;
using System.Diagnostics;

namespace PandaBear.Packages;

/// <summary>
/// Runs a real child process from an absolute path without a shell, in the invariant locale, with a time limit and a
/// limit on the standard output and on the standard error it keeps. Output above a limit ends the process.
/// </summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="executable"/> or <paramref name="arguments"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="executable"/> is not an absolute path.</exception>
    /// <exception cref="InvalidOperationException">The process cannot be started, writes more than the output limit, or leaves its output open after it exits.</exception>
    /// <exception cref="TimeoutException">The process runs longer than two minutes.</exception>
    public async Task<ProcessOutput> Run(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        if (!Path.IsPathFullyQualified(executable))
        {
            throw new ArgumentException("The executable must be an absolute path.", nameof(executable));
        }

        ProcessStartInfo startInfo = new(executable)
        {
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        // Tools print tables and messages in English: LC_ALL wins over the others, and LANGUAGE would override LC_MESSAGES.
        startInfo.Environment["LC_ALL"] = "C";
        startInfo.Environment["LC_MESSAGES"] = "C";
        startInfo.Environment["LANG"] = "C";
        startInfo.Environment.Remove("LANGUAGE");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var timeout = new CancellationTokenSource(Timeout);
        using var overflow = new CancellationTokenSource();
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token, overflow.Token);
        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Could not start the process.");
            }
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException("Could not start the process.", exception);
        }

        process.StandardInput.Close();

        // Declared after the process and the token sources, so it is disposed first and waits for the reads.
        var readers = ProcessStreamReaders.Start(process.StandardOutput, process.StandardError, overflow, execution.Token);
        await using var readersScope = readers.ConfigureAwait(false);
        var finished = false;
        try
        {
            await process.WaitForExitAsync(execution.Token).ConfigureAwait(false);

            // A descendant that inherited a pipe can keep it open after the process exited; do not wait for it forever.
            (string Output, string Error) text;
            try
            {
                text = await readers.Completed(DrainTimeout, execution.Token).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new InvalidOperationException("The process left its output open after it exited.");
            }

            finished = true;
            return new ProcessOutput(process.ExitCode, text.Output, text.Error);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            if (overflow.IsCancellationRequested)
            {
                throw new InvalidOperationException("The process wrote more output than the limit allows.");
            }

            throw new TimeoutException("The process did not finish within two minutes.");
        }
        finally
        {
            if (!finished)
            {
                // Stop the process tree and cancel the reads, so disposing the readers cannot wait on an open pipe, then wait
                // briefly for the tree to exit. A descendant that already left the tree and holds a pipe is not stopped.
                Stop(process);
                await execution.CancelAsync().ConfigureAwait(false);
                await WaitForExit(process).ConfigureAwait(false);
            }
        }
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            // The process ended between the check and the kill.
        }
    }

    private static async Task WaitForExit(Process process)
    {
        using var limit = new CancellationTokenSource(DrainTimeout);
        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The process did not exit within the limit; nothing more can be done here.
        }
    }
}
