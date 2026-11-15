using System.Diagnostics;
using System.Text;
using PandaBear.Processes;

namespace PandaBear.Versioning;

/// <summary>
/// Runs the external programs of the versioning engine, such as <c>git</c> and <c>dotnet</c>, through an
/// <see cref="IProcessRunner"/>, and reports a failed start or a non-zero exit as an <see cref="InvalidOperationException"/>.
/// </summary>
internal sealed class ExternalCommands
{
    private readonly IProcessRunner _processRunner;
    private readonly IExecutableResolver _executableResolver;

    /// <summary>Creates the commands from the runner that starts the processes and the resolver that finds bare command names.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="processRunner"/> or <paramref name="executableResolver"/> is <see langword="null"/>.</exception>
    public ExternalCommands(IProcessRunner processRunner, IExecutableResolver executableResolver)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(executableResolver);
        _processRunner = processRunner;
        _executableResolver = executableResolver;
    }

    /// <summary>Runs a process and returns its standard output; cancellation ends its process tree.</summary>
    /// <remarks>A bare command name resolves only through the executable resolver, which searches absolute <c>PATH</c> directories.</remarks>
    /// <exception cref="InvalidOperationException">The process could not start or exited with a non-zero code; the message holds its output.</exception>
    public async Task<string> Run(string fileName, IEnumerable<string> arguments, string? workingDirectory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var argumentList = arguments.ToArray();
        var result = await Start(CreateStartInfo(fileName, argumentList, workingDirectory), forward: null, ct).ConfigureAwait(false);
        if (result.ExitCode == 0)
        {
            return result.StandardOutput;
        }

        var command = new StringBuilder(fileName);
        foreach (var argument in argumentList)
        {
            command.Append(' ').Append(argument);
        }

        throw new InvalidOperationException(
            $"Command failed ({result.ExitCode}): {command}{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}{result.StandardError}");
    }

    /// <summary>
    /// Runs a process with <paramref name="environment"/> applied, where a <see langword="null"/> value removes a
    /// variable, and writes each line of its standard output and standard error to <paramref name="sink"/> as it
    /// arrives. Cancellation ends its process tree.
    /// </summary>
    /// <exception cref="InvalidOperationException">The process could not start or exited with a non-zero code.</exception>
    public async Task RunStreaming(
        string fileName, IReadOnlyList<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string?> environment, TextWriter sink, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var startInfo = CreateStartInfo(fileName, [.. arguments], workingDirectory);
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

        var result = await Start(startInfo, sink, ct).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Command failed ({result.ExitCode}): {fileName} {string.Join(' ', arguments)}");
        }
    }

    /// <summary>Runs a process like <see cref="Run"/>, but returns an empty string when it fails and cancellation was not requested.</summary>
    public async Task<string> RunOrDefault(string fileName, IEnumerable<string> arguments, string? workingDirectory, CancellationToken ct)
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

    // A start failure is the InvalidOperationException that IProcessRunner documents. SystemProcessRunner and
    // BoundedProcessRunner put the file name and the operating system's reason in its message; another runner may not.
    private Task<ProcessOutput> Start(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken ct) =>
        _processRunner.Run(startInfo, forward, ct);

    private ProcessStartInfo CreateStartInfo(string fileName, string[] arguments, string? workingDirectory)
    {
        var startInfo = new ProcessStartInfo(Resolve(fileName))
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

    private string Resolve(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return Path.IsPathFullyQualified(fileName)
            ? fileName
            : _executableResolver.Find(fileName) ?? throw new InvalidOperationException($"Failed to start {fileName}: no absolute PATH directory contains it.");
    }
}
