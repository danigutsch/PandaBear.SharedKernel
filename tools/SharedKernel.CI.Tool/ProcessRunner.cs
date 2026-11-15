using System.Diagnostics;
using System.Globalization;
using System.Text;
using PandaBear.Processes;

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
        Run("git", arguments, workingDirectory, GitTimeout, TimeProvider.System, ProcessEnvironment.RemoveGitVariables, ct);

    /// <summary>
    /// Runs a process with a configured start and a timeout that <paramref name="timeProvider"/> times, and returns its
    /// standard output.
    /// </summary>
    /// <exception cref="TimeoutException">The process did not exit within <paramref name="timeout"/>; its process tree is terminated first.</exception>
    /// <exception cref="InvalidOperationException">The process could not be started, or exited unsuccessfully.</exception>
    internal static async Task<string> Run(
        string fileName, string[] arguments, string workingDirectory, TimeSpan timeout, TimeProvider timeProvider, Action<ProcessStartInfo> configure, CancellationToken ct, bool echo = false)
    {
        var startInfo = new ProcessStartInfo(PathExecutableResolver.Default.Resolve(fileName))
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
        using var process = ProcessTree.Start(startInfo);
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
            await ProcessTree.Terminate(process, TerminationTimeout).ConfigureAwait(false);
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
}
