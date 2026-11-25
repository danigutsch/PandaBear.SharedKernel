using System.CommandLine;
using System.CommandLine.Help;
using System.Diagnostics;
using SharedKernel.CI.Tool;
using SharedKernel.Processes;

namespace SharedKernel.Repository.Tests;

/// <summary>Parses and invokes the real CI tool command tree with captured output.</summary>
internal static class RepositoryToolInvocation
{
    /// <summary>Invokes <paramref name="args"/> with <paramref name="run"/> as the process runner and offline staging services.</summary>
    public static Task<RepositoryToolResult> Run(
        string workingDirectory,
        IReadOnlyList<string> args,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        CancellationToken ct) =>
        Run(workingDirectory, args, run, CandidateStagingTestServices.Offline(new CandidateGit()), ct);

    /// <summary>Invokes <paramref name="args"/> with <paramref name="run"/> as the process runner and <paramref name="staging"/> as the stage-candidate services.</summary>
    public static Task<RepositoryToolResult> Run(
        string workingDirectory,
        IReadOnlyList<string> args,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        CandidateStagingServices staging,
        CancellationToken ct)
    {
        // Only stage-candidate reaches the staging Git; OpenAPI generation reaches the same recorder, and its environment is tested on its own.
        var git = args is ["stage-candidate", ..] ? staging.Git : null;
        var runner = new ScriptedProcessRunner(async (startInfo, _, token) => new ProcessOutput(0, await (git is not null && startInfo.FileName == "git"
            ? git([.. startInfo.ArgumentList], startInfo.WorkingDirectory, token)
            : run(startInfo.FileName, [.. startInfo.ArgumentList], startInfo.WorkingDirectory, token))));
        var services = new CiToolServices(
            new CiMachine(runner, new ExecutableMap(static command => command), TimeProvider.System),
            staging.CreateHttpHandler,
            staging.Limits,
            static () => throw new InvalidOperationException("Only prune-prereleases calls the API."),
            static _ => null,
            staging.TimeProvider);
        return Invoke(RepositoryCommandLine.Create(workingDirectory, services), args, ct);
    }

    /// <summary>
    /// Invokes with <paramref name="processes"/> as the process runner, so it records the environment of each process, and with
    /// <paramref name="startService"/> as the start of the service processes of a slice.
    /// </summary>
    public static Task<RepositoryToolResult> Run(
        string workingDirectory,
        IReadOnlyList<string> args,
        ProcessLog processes,
        Func<ProcessStartInfo, Process> startService,
        CancellationToken ct) =>
        Invoke(RepositoryCommandLine.Create(workingDirectory, Services(processes.Machine(startService: startService))), args, ct);

    /// <summary>Invokes <paramref name="args"/> with <paramref name="pruning"/> as the prune-prereleases services, and no process runner.</summary>
    public static Task<RepositoryToolResult> RunPruning(IReadOnlyList<string> args, PrereleasePruningServices pruning, CancellationToken ct)
    {
        var machine = new CiMachine(
            new ScriptedProcessRunner(static (_, _, _) => Task.FromException<ProcessOutput>(new InvalidOperationException("prune-prereleases starts no process."))),
            new ExecutableMap(static command => command),
            TimeProvider.System);
        return Invoke(RepositoryCommandLine.Create(Environment.CurrentDirectory, Services(machine) with
        {
            CreateApiHandler = pruning.CreateHttpHandler,
            GetEnvironmentVariable = pruning.GetEnvironmentVariable,
        }), args, ct);
    }

    /// <summary>Creates the command tree with processes that answer nothing, for tests that only parse.</summary>
    public static RootCommand CreateCommand(string workingDirectory) =>
        RepositoryCommandLine.Create(workingDirectory, Services(new ProcessLog(static (_, _, _) => Task.FromResult(string.Empty)).Machine()));

    private static CiToolServices Services(CiMachine machine) => new(
        machine,
        static () => new FakeReleaseServer(),
        CandidateDownloadLimits.Default,
        static () => throw new InvalidOperationException("Only prune-prereleases calls the API."),
        static _ => null);

    private static async Task<RepositoryToolResult> Invoke(RootCommand command, IReadOnlyList<string> args, CancellationToken ct)
    {
        // Help wraps at the width of the terminal running the tests; unwrapped help keeps assertions independent of it.
        var help = (HelpAction)command.Options.OfType<HelpOption>().First().Action!;
        help.MaxWidth = int.MaxValue;

        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Signal handlers are process-wide, so test invocations leave Ctrl+C and SIGTERM to the test host.
        var configuration = new InvocationConfiguration { Output = output, Error = error, ProcessTerminationTimeout = null };
        var exitCode = await command.Parse(args).InvokeAsync(configuration, ct);
        return new RepositoryToolResult(exitCode, output.ToString(), error.ToString());
    }
}
