using System.CommandLine;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Invokes the repo config command line in-process with captured output and a cancellation token.</summary>
internal static class RepoConfigToolRunner
{
    /// <summary>Runs the command line with a default environment rooted at <paramref name="workingDirectory"/>.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="output">The writer that receives standard output.</param>
    /// <param name="error">The writer that receives standard error.</param>
    /// <param name="workingDirectory">The working directory the tool environment resolves paths against.</param>
    /// <param name="ct">The token that cancels the invocation.</param>
    public static Task<int> Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        string workingDirectory,
        CancellationToken ct) =>
        Run(args, output, error, new RepoConfigToolEnvironment(workingDirectory), ct);

    /// <summary>Runs the command line with an environment that uses <paramref name="httpClient"/> for GitHub requests.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="output">The writer that receives standard output.</param>
    /// <param name="error">The writer that receives standard error.</param>
    /// <param name="workingDirectory">The working directory the tool environment resolves paths against.</param>
    /// <param name="httpClient">The HTTP client the tool uses, or <see langword="null"/> for the default.</param>
    /// <param name="ct">The token that cancels the invocation.</param>
    public static Task<int> Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        string workingDirectory,
        HttpClient? httpClient,
        CancellationToken ct) =>
        Run(args, output, error, new RepoConfigToolEnvironment(workingDirectory) { HttpClient = httpClient }, ct);

    /// <summary>Runs the command line with an environment that uses <paramref name="httpClient"/> and <paramref name="gitRepository"/> instead of real GitHub and Git access.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="output">The writer that receives standard output.</param>
    /// <param name="error">The writer that receives standard error.</param>
    /// <param name="workingDirectory">The working directory the tool environment resolves paths against.</param>
    /// <param name="httpClient">The HTTP client the tool uses, or <see langword="null"/> for the default.</param>
    /// <param name="gitRepository">The Git repository abstraction the tool uses, or <see langword="null"/> for the default.</param>
    /// <param name="ct">The token that cancels the invocation.</param>
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

    /// <summary>Parses and invokes the command line against <paramref name="environment"/> without installing process-wide signal handlers.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="output">The writer that receives standard output.</param>
    /// <param name="error">The writer that receives standard error.</param>
    /// <param name="environment">The tool environment the commands run against.</param>
    /// <param name="ct">The token that cancels the invocation.</param>
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
