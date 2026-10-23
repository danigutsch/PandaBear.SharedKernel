using System.CommandLine;

namespace PandaBear.RepoConfig.Tests;

internal static class RepoConfigToolRunner
{
    public static Task<int> Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        string workingDirectory,
        CancellationToken ct) =>
        Run(args, output, error, new RepoConfigToolEnvironment(workingDirectory), ct);

    public static Task<int> Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        string workingDirectory,
        HttpClient? httpClient,
        CancellationToken ct) =>
        Run(args, output, error, new RepoConfigToolEnvironment(workingDirectory) { HttpClient = httpClient }, ct);

    public static Task<int> Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        string workingDirectory,
        HttpClient? httpClient,
        RoadmapGitRepository? gitRepository,
        CancellationToken ct) =>
        Run(
            args,
            output,
            error,
            new RepoConfigToolEnvironment(workingDirectory) { HttpClient = httpClient, GitRepository = gitRepository },
            ct);

    public static Task<int> Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        RepoConfigToolEnvironment environment,
        CancellationToken ct) =>
        RepoConfigCommandLine.Create(environment)
            .Parse(args)
            .InvokeAsync(
                new InvocationConfiguration
                {
                    Output = output,
                    Error = error,
                    // Signal handlers are process-wide, so tests cancel through the token.
                    ProcessTerminationTimeout = null,
                },
                ct);
}
