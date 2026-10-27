using System.CommandLine;
using System.CommandLine.Help;
using System.Diagnostics;
using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

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
        // OpenAPI generation reaches the same recorder; its environment is tested on its own.
        var command = RepositoryCommandLine.Create(workingDirectory, run, (fileName, arguments, directory, _, token) => run(fileName, arguments, directory, token), staging);
        return Invoke(command, args, ct);
    }

    /// <summary>
    /// Invokes with <paramref name="processes"/> as both runners, so it records the environment of a configured process, and with
    /// <paramref name="startService"/> as the start of the service processes of a slice.
    /// </summary>
    public static Task<RepositoryToolResult> Run(
        string workingDirectory,
        IReadOnlyList<string> args,
        ProcessLog processes,
        Func<ProcessStartInfo, Process> startService,
        CancellationToken ct) =>
        Invoke(
            RepositoryCommandLine.Create(
                workingDirectory, processes.Run, processes.RunConfigured, CandidateStagingTestServices.Offline(new CandidateGit()), startService),
            args,
            ct);

    private static async Task<RepositoryToolResult> Invoke(RootCommand command, IReadOnlyList<string> args, CancellationToken ct)
    {
        // Help wraps at the width of the terminal running the tests; unwrapped help keeps assertions independent of it.
        var help = (HelpAction)command.Options.OfType<HelpOption>().First().Action!;
        help.MaxWidth = int.MaxValue;

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Signal handlers are process-wide, so test invocations leave Ctrl+C and SIGTERM to the test host.
        var configuration = new InvocationConfiguration { Output = output, Error = error, ProcessTerminationTimeout = null };
        var exitCode = await command.Parse(args).InvokeAsync(configuration, ct);
        return new RepositoryToolResult(exitCode, output.ToString(), error.ToString());
    }
}
