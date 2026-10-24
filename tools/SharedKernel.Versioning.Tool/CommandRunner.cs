using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using PandaBear.Tooling;

namespace PandaBear.Versioning.Tool;

internal static class CommandRunner
{
    private static readonly TimeSpan TerminationTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Runs a process and kills its process tree when <paramref name="ct" /> is canceled.</summary>
    /// <remarks>A bare command name resolves only from the absolute directories of <c>PATH</c>.</remarks>
    public static async Task<string> Run(string fileName, IEnumerable<string> arguments, string? workingDirectory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var argumentList = arguments.ToArray();
        using var process = StartProcess(CreateStartInfo(ExecutableResolver.ResolveCommand(fileName), argumentList, workingDirectory), fileName);
        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await Terminate(process).ConfigureAwait(false);
            throw;
        }

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        EnsureSuccess(process, fileName, argumentList, output, error);
        return output;
    }

    public static async Task<string> RunOrDefault(string fileName, IEnumerable<string> arguments, string? workingDirectory, CancellationToken ct)
    {
        try
        {
            return await Run(fileName, arguments, workingDirectory, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException) when (!ct.IsCancellationRequested)
        {
            return string.Empty;
        }
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

    private static ProcessStartInfo CreateStartInfo(string fileName, string[] arguments, string? workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void EnsureSuccess(Process process, string fileName, string[] arguments, string output, string error)
    {
        if (process.ExitCode == 0)
        {
            return;
        }

        var command = new StringBuilder(fileName);
        foreach (var argument in arguments)
        {
            command.Append(' ').Append(argument);
        }

        throw new InvalidOperationException($"Command failed ({process.ExitCode}): {command}{Environment.NewLine}{output}{Environment.NewLine}{error}");
    }

    private static Process StartProcess(ProcessStartInfo startInfo, string fileName)
    {
        try
        {
            return Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {fileName}.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to start {fileName}.", ex);
        }
    }
}
