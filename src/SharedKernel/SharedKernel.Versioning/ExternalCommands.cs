using System.Diagnostics;
using System.Globalization;
using System.Text;
using SharedKernel.Processes;

namespace SharedKernel.Versioning;

/// <summary>
/// Runs the external programs of the versioning engine, such as <c>git</c> and <c>dotnet</c>, through an
/// <see cref="IProcessRunner"/>, and reports a failed start or a non-zero exit as an <see cref="InvalidOperationException"/>.
/// Every <c>git</c> run gets the environment of <see cref="GitStartInfo.ApplyEnvironment"/> and a time limit; when the
/// runner is a <see cref="SystemProcessRunner"/>, which has no output limit, <c>git</c> runs through a
/// <see cref="BoundedProcessRunner"/> instead.
/// </summary>
internal sealed class ExternalCommands
{
    private readonly IProcessRunner _processRunner;
    private readonly IProcessRunner _gitRunner;
    private readonly IExecutableResolver _executableResolver;
    private readonly TimeSpan _gitTimeout;

    /// <summary>Creates the commands from the runner that starts the processes and the resolver that finds bare command names.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="processRunner"/> or <paramref name="executableResolver"/> is <see langword="null"/>.</exception>
    public ExternalCommands(IProcessRunner processRunner, IExecutableResolver executableResolver)
        : this(processRunner, executableResolver, GitStartInfo.Timeout)
    {
    }

    /// <summary>Creates the commands, giving each <c>git</c> run <paramref name="gitTimeout"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="processRunner"/> or <paramref name="executableResolver"/> is <see langword="null"/>.</exception>
    internal ExternalCommands(IProcessRunner processRunner, IExecutableResolver executableResolver, TimeSpan gitTimeout)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(executableResolver);
        _processRunner = processRunner;
        _gitRunner = processRunner is SystemProcessRunner ? new BoundedProcessRunner() : processRunner;
        _executableResolver = executableResolver;
        _gitTimeout = gitTimeout;
    }

    /// <summary>Runs a process and returns its standard output; cancellation ends its process tree.</summary>
    /// <remarks>A bare command name resolves only through the executable resolver, which searches absolute <c>PATH</c> directories.</remarks>
    /// <exception cref="InvalidOperationException">
    /// The process could not start, wrote more than a bounded runner allows, or exited with a non-zero code, and the message
    /// holds its output; or <c>git</c> did not exit within its time limit, and its process tree has been ended.
    /// </exception>
    public async Task<string> Run(string fileName, IEnumerable<string> arguments, string? workingDirectory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var argumentList = arguments.ToArray();
        // A start failure is the InvalidOperationException that IProcessRunner documents. SystemProcessRunner and
        // BoundedProcessRunner put the file name and the operating system's reason in its message; another runner may not.
        var result = await RunWithExitCode(fileName, argumentList, workingDirectory, ct).ConfigureAwait(false);
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

    /// <summary>Runs a process and captures its result, including nonzero exit codes.</summary>
    internal Task<ProcessOutput> RunWithExitCode(string fileName, string[] arguments, string? workingDirectory, CancellationToken ct) =>
        fileName == "git"
            ? RunGit(arguments, workingDirectory, ct)
            : _processRunner.Run(CreateStartInfo(fileName, arguments, workingDirectory), forward: null, ct);

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

        var result = await _processRunner.Run(startInfo, sink, ct).ConfigureAwait(false);
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

    private async Task<ProcessOutput> RunGit(string[] arguments, string? workingDirectory, CancellationToken ct)
    {
        var startInfo = CreateStartInfo("git", arguments, workingDirectory);
        GitStartInfo.ApplyEnvironment(startInfo);
        using var timeout = new CancellationTokenSource(_gitTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            return await _gitRunner.Run(startInfo, forward: null, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"git {string.Join(' ', arguments)} did not exit within {_gitTimeout.TotalSeconds} seconds."),
                exception);
        }
    }

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
