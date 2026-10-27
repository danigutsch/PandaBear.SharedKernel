namespace PandaBear.Versioning.Tool;

/// <summary>Calculates the next release version from the commits since the last release tag.</summary>
internal static class ReleaseVersionCommand
{
    /// <summary>Reads the source SHA, last tag, and commit messages from git, calculates the version, writes the requested GitHub outputs, and returns it.</summary>
    public static async Task<VersionOutput> Run(CalculateReleaseOptions options, TextWriter output, CancellationToken ct)
    {
        if (options.VersionKind == "prerelease")
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(options.RunNumber);
        }

        var sourceSha = string.IsNullOrWhiteSpace(options.Sha)
            ? (await CommandRunner.Run("git", ["rev-parse", "HEAD"], options.RepoRoot, ct).ConfigureAwait(false)).Trim()
            : options.Sha;
        var sourceTag = (await CommandRunner.RunOrDefault(
            "git",
            ["describe", "--tags", "--match", "v[0-9]*", "--abbrev=0"],
            options.RepoRoot,
            ct).ConfigureAwait(false)).Trim();

        var commitRange = ReleaseVersionCalculator.GetCommitRange(sourceTag);
        var logOutput = await CommandRunner.Run("git", ["log", "--format=%B%x00", commitRange], options.RepoRoot, ct).ConfigureAwait(false);
        var result = await ReleaseVersionCalculator.Calculate(
            new ReleaseVersionCalculationInput(
                sourceSha,
                sourceTag,
                logOutput,
                options.VersionKind,
                options.RunNumber)).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(options.GitHubOutput))
        {
            await File.AppendAllLinesAsync(
                options.GitHubOutput,
                ReleaseVersionCalculator.CreateGitHubOutputs(result),
                ct).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(options.GitHubSummary))
        {
            await File.AppendAllLinesAsync(options.GitHubSummary, ReleaseVersionCalculator.CreateSummary(result), ct).ConfigureAwait(false);
        }

        await output.WriteLineAsync(result.VersionJson).ConfigureAwait(false);
        return result.VersionOutput;
    }
}
