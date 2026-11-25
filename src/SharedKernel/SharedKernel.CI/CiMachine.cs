using System.Diagnostics;
using System.Globalization;
using SharedKernel.Processes;

namespace SharedKernel.CI;

/// <summary>
/// The side effects of the CI engine: how it runs and finds child processes such as <c>git</c> and <c>dotnet</c>, how it
/// starts the long-running service of a slice, and how it tells time. Hosts pass the system implementations; tests pass
/// fakes. Every <c>git</c> run gets the environment of <see cref="GitStartInfo.ApplyEnvironment"/> and
/// <see cref="GitStartInfo.Timeout"/>; when the runner is a <see cref="SystemProcessRunner"/>, which has no output limit,
/// <c>git</c> runs through a <see cref="BoundedProcessRunner"/> instead.
/// </summary>
public sealed class CiMachine
{

    /// <summary>Creates the machine from the process runner, the executable resolver, and the clock.</summary>
    /// <param name="processRunner">Runs a child process to completion, such as <see cref="SystemProcessRunner"/>.</param>
    /// <param name="executableResolver">Finds a bare command name, such as <see cref="PathExecutableResolver.Default"/>, which searches only the absolute <c>PATH</c> directories.</param>
    /// <param name="timeProvider">Times the runs, their timeouts, and the evidence, such as <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public CiMachine(IProcessRunner processRunner, IExecutableResolver executableResolver, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(executableResolver);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ProcessRunner = processRunner;
        ExecutableResolver = executableResolver;
        TimeProvider = timeProvider;
        GitRunner = processRunner is SystemProcessRunner ? new BoundedProcessRunner() : processRunner;
    }

    /// <summary>Gets the runner of child processes.</summary>
    public IProcessRunner ProcessRunner { get; }

    /// <summary>Gets the resolver of bare command names.</summary>
    public IExecutableResolver ExecutableResolver { get; }

    /// <summary>Gets the clock.</summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>Gets the runner of <c>git</c>: a bounded runner in place of a <see cref="SystemProcessRunner"/>, otherwise <see cref="ProcessRunner"/>.</summary>
    internal IProcessRunner GitRunner { get; }

    /// <summary>Gets the time limit of one <c>git</c> run; <see cref="GitStartInfo.Timeout"/> unless a test shortens it.</summary>
    internal TimeSpan GitTimeout { get; init; } = GitStartInfo.Timeout;

    /// <summary>
    /// Gets the start of a slice's service process from its start information. The default,
    /// <see cref="ProcessTree.Start(ProcessStartInfo)"/>, starts it in its own process group so that its whole tree can be ended.
    /// </summary>
    public Func<ProcessStartInfo, Process> StartService { get; init; } = ProcessTree.Start;

    /// <summary>Runs a process and returns its standard output.</summary>
    /// <exception cref="InvalidOperationException">The process could not start or exited with a non-zero code.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled; the process tree is terminated first.</exception>
    internal Task<string> Run(string fileName, string[] arguments, string workingDirectory, CancellationToken ct) =>
        Run(fileName, arguments, workingDirectory, Timeout.InfiniteTimeSpan, static _ => { }, null, ct);

    /// <summary>Runs a process whose start information <paramref name="configure"/> adjusts, and returns its standard output.</summary>
    /// <exception cref="InvalidOperationException">The process could not start or exited with a non-zero code.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled; the process tree is terminated first.</exception>
    internal Task<string> RunConfigured(string fileName, string[] arguments, string workingDirectory, Action<ProcessStartInfo> configure, CancellationToken ct) =>
        Run(fileName, arguments, workingDirectory, Timeout.InfiniteTimeSpan, configure, null, ct);

    /// <summary>
    /// Runs a process and writes each of its output lines to <paramref name="echo"/> with its UTC time as the line arrives,
    /// so a long step shows where it spends its time, and returns the lines it wrote.
    /// </summary>
    /// <exception cref="InvalidOperationException">The process could not start or exited with a non-zero code.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled; the process tree is terminated first.</exception>
    internal Task<string> RunStreaming(string fileName, string[] arguments, string workingDirectory, TextWriter echo, CancellationToken ct) =>
        Run(fileName, arguments, workingDirectory, Timeout.InfiniteTimeSpan, static _ => { }, echo, ct);

    /// <summary>
    /// Runs Git as <see cref="Run(string, string[], string, CancellationToken)"/> runs it: without a prompt or the inherited
    /// <c>GIT_*</c> variables, which can point it at another repository, with bounded output, and stopped when it has not
    /// exited within <see cref="GitTimeout"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Git could not start, wrote more output than the limit, or exited with a non-zero code.</exception>
    /// <exception cref="TimeoutException">Git did not exit within <see cref="GitTimeout"/>; its process tree is terminated first.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled; the process tree is terminated first.</exception>
    internal Task<string> RunGit(string[] arguments, string workingDirectory, CancellationToken ct) =>
        Run("git", arguments, workingDirectory, ct);

    /// <summary>Returns <paramref name="fileName"/> when it is fully qualified, and otherwise finds it with the resolver.</summary>
    /// <exception cref="InvalidOperationException">The resolver does not find the command.</exception>
    internal string Resolve(string fileName) =>
        Path.IsPathFullyQualified(fileName)
            ? fileName
            : ExecutableResolver.Find(fileName) ?? throw new InvalidOperationException($"Failed to start {fileName}: no absolute PATH directory contains it.");

    /// <summary>
    /// Runs a process with a configured start and a timeout, and returns its standard output. A <c>git</c> run gets the
    /// environment of <see cref="GitStartInfo.ApplyEnvironment"/> before <paramref name="configure"/>, the shorter of
    /// <paramref name="timeout"/> and <see cref="GitTimeout"/>, and <see cref="GitRunner"/>.
    /// </summary>
    /// <exception cref="TimeoutException">The process did not exit within its time limit; its process tree is terminated first.</exception>
    /// <exception cref="InvalidOperationException">The process could not be started, or exited unsuccessfully.</exception>
    internal async Task<string> Run(
        string fileName,
        string[] arguments,
        string workingDirectory,
        TimeSpan timeout,
        Action<ProcessStartInfo> configure,
        TextWriter? echo,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(Resolve(fileName))
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

        var git = fileName == "git";
        if (git)
        {
            GitStartInfo.ApplyEnvironment(startInfo);
            timeout = timeout == Timeout.InfiniteTimeSpan || timeout > GitTimeout ? GitTimeout : timeout;
        }

        configure(startInfo);
        // Without a timeout the runner gets the caller's token itself, and no timer is created.
        using var timeoutSource = timeout == Timeout.InfiniteTimeSpan ? null : new CancellationTokenSource(timeout, TimeProvider);
        using var linked = timeoutSource is null ? null : CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutSource.Token);
        using var transcript = echo is null ? null : new TimestampedTranscript(echo, TimeProvider);
        ProcessOutput result;
        try
        {
            result = await (git ? GitRunner : ProcessRunner).Run(startInfo, transcript, linked?.Token ?? ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (timeoutSource is { IsCancellationRequested: true } && !ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                string.Create(CultureInfo.InvariantCulture, $"{fileName} {string.Join(' ', arguments)} did not exit within {timeout.TotalSeconds} seconds."),
                exception);
        }

        // A forwarding runner returns no output, so the transcript holds it; a runner that ignores the writer returns it.
        var output = transcript is null ? result.StandardOutput : transcript.Text + result.StandardOutput;
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Command failed ({result.ExitCode}): {fileName} {string.Join(' ', arguments)}{Environment.NewLine}{output}{Environment.NewLine}{result.StandardError}");
        }

        return output;
    }
}
