using System.Diagnostics;
using System.Text.Json;
using PandaBear.Processes;

namespace PandaBear.Repositories;

/// <summary>Compares restored solution dependencies against current SDK evaluations.</summary>
public static class NuGetRestoreGraphComparison
{
    /// <summary>
    /// Writes a deterministic report for two solutions restored with locked mode enabled. Reports unavailable evidence
    /// when a restore is missing or its inputs differ from the current SDK evaluation. Runs no builds or tests.
    /// </summary>
    /// <param name="options">The checkouts, output path, and optional CI receipts.</param>
    /// <param name="output">The human-readable report summary.</param>
    /// <param name="error">Diagnostics when the evidence is incomplete or the command cannot run.</param>
    /// <param name="cancellationToken">Cancels evaluation and graph reading.</param>
    /// <returns>Zero for a complete report, or one if evidence is unavailable or the comparison failed.</returns>
    /// <exception cref="ArgumentNullException">The options or an output writer is null.</exception>
    public static async Task<int> Run(
        NuGetRestoreGraphComparisonOptions options, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            var root = Path.GetFullPath(options.BaselineRoot);
            var destination = Path.GetFullPath(options.OutputPath, root);
            RollbackFileWriteBatch.ValidateWritePath(root, destination);
            var baseline = await Read(root, options.BaselineVerification, cancellationToken).ConfigureAwait(false);
            var candidateRoot = Path.GetFullPath(options.CandidateRoot, root);
            var candidate = await Read(candidateRoot, options.CandidateVerification, cancellationToken).ConfigureAwait(false);
            await ReadFileAppGraphs(root, baseline, cancellationToken).ConfigureAwait(false);
            await ReadFileAppGraphs(candidateRoot, candidate, cancellationToken).ConfigureAwait(false);
            var report = Compare(baseline, candidate);
            var json = JsonSerializer.Serialize(report, NuGetRestoreGraphJsonContext.Default.NuGetRestoreGraphReport);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            RollbackFileWriteBatch.Apply(root, [new AtomicFileWrite(destination, json + "\n", null)]);

            var unavailable = report.Projects.Count(project => project.BaselineUnavailable is not null || project.CandidateUnavailable is not null);
            var changed = report.Projects.Sum(project => project.Changes.Count);
            await output.WriteLineAsync(
                $"Compared {baseline.Revision} (SDK {baseline.SdkVersion}) with {candidate.Revision} (SDK {candidate.SdkVersion})."
                    .AsMemory(), cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(FormattableString.Invariant(
                $"{changed} package changes across {report.Projects.Count} projects.").AsMemory(), cancellationToken).ConfigureAwait(false);
            if (unavailable != 0)
            {
                await error.WriteLineAsync(FormattableString.Invariant(
                    $"Restore evidence is unavailable for {unavailable} projects. See the comparison report.").AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            return unavailable == 0 ? 0 : 1;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException
            or IOException or UnauthorizedAccessException or JsonException or TimeoutException)
        {
            await error.WriteLineAsync(
                "Could not evaluate the restored solutions or write the comparison report.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task ReadFileAppGraphs(string root, NuGetRestoreGraphSource source, CancellationToken ct)
    {
        foreach (var file in await NuGetFileApps.Find(root, ct).ConfigureAwait(false))
        {
            source.Projects.Add(file, await NuGetFileApps.ReadGraph(root, file, [], ct).ConfigureAwait(false));
        }
    }

    /// <summary>Reads evaluated projects, restore evidence and provenance for the checkout.</summary>
    /// <exception cref="InvalidOperationException">The repository SDK or solution graph is unavailable.</exception>
    internal static async Task<NuGetRestoreGraphSource> Read(
        string root, string? receipt, CancellationToken cancellationToken, string[]? evaluationOptions = null)
    {
        var git = new RoadmapGitRepository();
        var revision = await git.ReadHead(root, cancellationToken).ConfigureAwait(false);
        var dirty = (await git.ReadStatus(root, cancellationToken).ConfigureAwait(false)).Length != 0;
        var solution = await FindSolution(root, revision, cancellationToken).ConfigureAwait(false);
        var runner = new BoundedProcessRunner();
        var version = await RunDotnet(root, runner, ["--version"], cancellationToken).ConfigureAwait(false);
        var sdk = version.StandardOutput.Trim();
        if (version.ExitCode != 0 || sdk.Length == 0 || !sdk.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-'))
        {
            throw new InvalidOperationException("The repository SDK could not be selected.");
        }

        var directory = Directory.CreateTempSubdirectory("sharedkernel-restore-graph-");
        try
        {
            var spec = Path.Combine(directory.FullName, "restore.json");
            var evaluation = await RunDotnet(root, runner,
                ["msbuild", Path.GetFullPath(solution, root), "-nologo", "-nodeReuse:false", "-t:GenerateRestoreGraphFile",
                    "-p:RestoreGraphOutputPath=" + spec, "-p:RestoreLockedMode=true", .. evaluationOptions ?? []],
                cancellationToken).ConfigureAwait(false);
            if (evaluation.ExitCode != 0)
            {
                throw new InvalidOperationException("MSBuild could not evaluate the repository restore graph.");
            }

            var projects = await NuGetRestoreGraphReader.Read(root, spec, cancellationToken).ConfigureAwait(false);
            if (projects.Count == 0)
            {
                throw new InvalidOperationException("The evaluated solution contains no restore projects.");
            }

            var verification = await NuGetRestoreVerificationReader.Read(root, receipt, revision, dirty, cancellationToken).ConfigureAwait(false);
            return new(revision, dirty, sdk, solution, projects, verification);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Runs the selected .NET SDK without forge credentials.</summary>
    /// <exception cref="InvalidOperationException">The .NET SDK executable cannot be found.</exception>
    internal static Task<ProcessOutput> RunDotnet(
        string root, IProcessRunner runner, string[] arguments, CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var dotnet = PathExecutableResolver.Default.Find("dotnet")
            ?? throw new InvalidOperationException("Could not resolve the .NET SDK executable.");
        var start = new ProcessStartInfo(dotnet)
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        ProcessEnvironment.RemoveForgeTokens(start);
        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                start.Environment[key] = value;
            }
        }

        return runner.Run(start, forward: null, cancellationToken);
    }

    /// <summary>Gets the single repository solution at the selected revision.</summary>
    internal static async Task<string> FindSolution(string root, string revision, CancellationToken ct)
    {
        var paths = await new RoadmapGitRepository().ReadCommittedRootPaths(root, revision, ct).ConfigureAwait(false);
        return paths.Single(path =>
            path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Compares the resolved package graphs by project and target.</summary>
    internal static NuGetRestoreGraphReport Compare(NuGetRestoreGraphSource baseline, NuGetRestoreGraphSource candidate)
    {
        var names = baseline.Projects.Keys.Union(candidate.Projects.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal);
        List<NuGetRestoreProjectReport> projects = [];
        foreach (var name in names)
        {
            var before = baseline.Projects.GetValueOrDefault(name);
            var after = candidate.Projects.GetValueOrDefault(name);
            var changes = before?.Unavailable is null && after?.Unavailable is null
                ? (before?.Graph ?? NuGetRestoreGraph.Empty).Compare(after?.Graph ?? NuGetRestoreGraph.Empty)
                : [];
            projects.Add(new(name, before?.LockExempt, after?.LockExempt, before?.Unavailable, after?.Unavailable, changes));
        }

        return new(1, baseline.Revision, baseline.Dirty, baseline.SdkVersion, baseline.Verification,
            candidate.Revision, candidate.Dirty, candidate.SdkVersion, candidate.Verification, projects);
    }
}
