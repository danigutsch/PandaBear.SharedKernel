using System.Diagnostics;
using System.Text;
using PandaBear.Processes;

namespace PandaBear.Versioning.Tool;

/// <summary>Runs external processes and captures their output.</summary>
internal static class CommandRunner
{
    private static readonly TimeSpan TerminationTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Runs a process and kills its process tree when <paramref name="ct" /> is canceled.</summary>
    /// <remarks>A bare command name resolves only from the absolute directories of <c>PATH</c>.</remarks>
    public static async Task<string> Run(string fileName, IEnumerable<string> arguments, string? workingDirectory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var argumentList = arguments.ToArray();
        using var process = ProcessTree.Start(CreateStartInfo(PathExecutableResolver.Default.Resolve(fileName), argumentList, workingDirectory));
        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await ProcessTree.Terminate(process, TerminationTimeout).ConfigureAwait(false);
            throw;
        }

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        EnsureSuccess(process, fileName, argumentList, output, error);
        return output;
    }

    /// <summary>
    /// Runs a process with <paramref name="environment"/> applied, where a <see langword="null"/> value removes a
    /// variable, and writes each line of its standard output and standard error to <paramref name="sink"/> as it
    /// arrives. Kills its process tree when <paramref name="ct" /> is canceled.
    /// </summary>
    /// <remarks>A bare command name resolves only from the absolute directories of <c>PATH</c>.</remarks>
    /// <exception cref="InvalidOperationException">The process could not start or exited with a non-zero code.</exception>
    public static async Task RunStreaming(
        string fileName, IReadOnlyList<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string?> environment, TextWriter sink, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var startInfo = CreateStartInfo(PathExecutableResolver.Default.Resolve(fileName), [.. arguments], workingDirectory);
        foreach (var (name, value) in environment)
        {
            if (value is null)
            {
                startInfo.Environment.Remove(name);
            }
            else
            {
                startInfo.Environment[name] = value;
            }
        }

        using var process = ProcessTree.Start(startInfo);
        var synchronized = TextWriter.Synchronized(sink);
        var pumps = Task.WhenAll(Pump(standardError: false), Pump(standardError: true));
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await ProcessTree.Terminate(process, TerminationTimeout).ConfigureAwait(false);

            // The killed process tree closes its pipes; a descendant that escaped it cannot hold the cancellation.
            await pumps.WaitAsync(TerminationTimeout, CancellationToken.None).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            throw;
        }

        await pumps.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Command failed ({process.ExitCode}): {fileName} {string.Join(' ', arguments)}");
        }

        async Task Pump(bool standardError)
        {
            var reader = standardError ? process.StandardError : process.StandardOutput;
            while (await reader.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
            {
                await synchronized.WriteLineAsync(line).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Runs a process like Run, but returns an empty string when it fails with an invalid operation and cancellation was not requested.</summary>
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
}
