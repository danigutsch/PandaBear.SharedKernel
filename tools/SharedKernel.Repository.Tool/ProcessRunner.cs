using System.ComponentModel;
using System.Diagnostics;

namespace PandaBear.Repository.Tool;

/// <summary>Runs child processes and stops them when the command is cancelled.</summary>
internal static class ProcessRunner
{
    private static readonly TimeSpan TerminationTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Runs a process and returns its standard output.</summary>
    /// <exception cref="InvalidOperationException">The process could not start or exited with a non-zero code.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled; the process tree is terminated first.</exception>
    public static async Task<string> Run(string fileName, string[] arguments, string workingDirectory, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Start(startInfo);
        var standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await Terminate(process).ConfigureAwait(false);
            throw;
        }

        var output = await standardOutput.ConfigureAwait(false);
        var error = await standardError.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Command failed ({process.ExitCode}): {fileName} {string.Join(' ', arguments)}{Environment.NewLine}{output}{Environment.NewLine}{error}");
        }

        return output;
    }

    /// <summary>Kills the process tree unless the process has exited, and waits up to two seconds for it to exit.</summary>
    /// <remarks>Termination failures are ignored, so the cancellation that started cleanup reaches the caller.</remarks>
    internal static async Task Terminate(Process process)
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
            // Best-effort cleanup must not replace the caller's cancellation.
        }
    }

    private static Process Start(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {startInfo.FileName}.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException($"Failed to start {startInfo.FileName}.", exception);
        }
    }
}
