using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>Runs child processes and stops them when the command is cancelled.</summary>
internal static class ProcessRunner
{
    private static readonly TimeSpan TerminationTimeout = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(1);

    /// <summary>Runs a process and returns its standard output.</summary>
    /// <remarks>A bare command name resolves only from the absolute directories of <c>PATH</c>.</remarks>
    /// <exception cref="InvalidOperationException">The process could not start or exited with a non-zero code.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled; the process tree is terminated first.</exception>
    public static Task<string> Run(string fileName, string[] arguments, string workingDirectory, CancellationToken ct) =>
        Run(fileName, arguments, workingDirectory, Timeout.InfiniteTimeSpan, TimeProvider.System, static _ => { }, ct);

    /// <summary>
    /// Runs a process and returns its standard output, writing each output line of the process to standard error with its UTC
    /// time as the line arrives, so a long step shows where it spends its time.
    /// </summary>
    /// <exception cref="InvalidOperationException">The process could not start or exited with a non-zero code.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled; the process tree is terminated first.</exception>
    public static Task<string> RunStreaming(string fileName, string[] arguments, string workingDirectory, CancellationToken ct) =>
        Run(fileName, arguments, workingDirectory, Timeout.InfiniteTimeSpan, TimeProvider.System, static _ => { }, ct, echo: true);

    /// <summary>Runs a process whose start information <paramref name="configure"/> adjusts, and returns its standard output.</summary>
    /// <exception cref="InvalidOperationException">The process could not start or exited with a non-zero code.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled; the process tree is terminated first.</exception>
    public static Task<string> RunConfigured(string fileName, string[] arguments, string workingDirectory, Action<ProcessStartInfo> configure, CancellationToken ct) =>
        Run(fileName, arguments, workingDirectory, Timeout.InfiniteTimeSpan, TimeProvider.System, configure, ct);

    /// <summary>
    /// Runs Git without the inherited <c>GIT_*</c> variables, which can point it at another repository, and stops it
    /// when it has not exited within a minute.
    /// </summary>
    /// <exception cref="InvalidOperationException">Git could not start or exited with a non-zero code.</exception>
    /// <exception cref="TimeoutException">Git did not exit within a minute; its process tree is terminated first.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled; the process tree is terminated first.</exception>
    public static Task<string> RunGit(string[] arguments, string workingDirectory, CancellationToken ct) =>
        Run("git", arguments, workingDirectory, GitTimeout, TimeProvider.System, RemoveGitVariables, ct);

    /// <summary>Removes every <c>GIT_*</c> variable, in any case, from the child environment.</summary>
    internal static void RemoveGitVariables(ProcessStartInfo startInfo)
    {
        foreach (var name in startInfo.Environment.Keys.Where(static name => name.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            startInfo.Environment.Remove(name);
        }
    }

    /// <summary>
    /// Runs a process with a configured start and a timeout that <paramref name="timeProvider"/> times, and returns its
    /// standard output.
    /// </summary>
    /// <exception cref="TimeoutException">The process did not exit within <paramref name="timeout"/>; its process tree is terminated first.</exception>
    internal static async Task<string> Run(
        string fileName, string[] arguments, string workingDirectory, TimeSpan timeout, TimeProvider timeProvider, Action<ProcessStartInfo> configure, CancellationToken ct, bool echo = false)
    {
        var startInfo = new ProcessStartInfo(ExecutableResolver.ResolveCommand(fileName))
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

        configure(startInfo);
        using var process = Start(startInfo);
        var standardOutput = echo ? ReadEchoed(process.StandardOutput, timeProvider) : process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardError = echo ? ReadEchoed(process.StandardError, timeProvider) : process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            await Terminate(process).ConfigureAwait(false);
            if (timeoutSource.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                throw new TimeoutException(
                    string.Create(CultureInfo.InvariantCulture, $"{fileName} {string.Join(' ', arguments)} did not exit within {timeout.TotalSeconds} seconds."),
                    exception);
            }

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

    /// <summary>Reads a stream to its end, writing each line to standard error with its UTC time as it arrives.</summary>
    private static async Task<string> ReadEchoed(StreamReader reader, TimeProvider timeProvider)
    {
        var text = new StringBuilder();
        while (await reader.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
        {
            text.AppendLine(line);
            await Console.Error.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"[{timeProvider.GetUtcNow():HH:mm:ss}] {line}")).ConfigureAwait(false);
        }

        return text.ToString();
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

    /// <summary>Starts a process, reporting a start failure as an invalid operation.</summary>
    /// <exception cref="InvalidOperationException">The process could not start.</exception>
    internal static Process Start(ProcessStartInfo startInfo)
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
