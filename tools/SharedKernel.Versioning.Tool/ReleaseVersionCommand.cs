namespace SharedKernel.Versioning.Tool;

internal static class ReleaseVersionCommand
{
    public static async Task<VersionOutput> Run(CalculateReleaseOptions options, TextWriter output)
    {
        var sourceSha = string.IsNullOrWhiteSpace(options.Sha)
            ? CommandRunner.Run("git", ["rev-parse", "HEAD"], options.RepoRoot).Trim()
            : options.Sha;
        var sourceTag = CommandRunner.RunOrDefault(
            "git",
            ["describe", "--tags", "--match", "v[0-9]*", "--abbrev=0"],
            options.RepoRoot).Trim();

        var commitRange = ReleaseVersionCalculator.GetCommitRange(sourceTag);
        var logOutput = CommandRunner.Run("git", ["log", "--format=%B%x00", commitRange], options.RepoRoot);
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
                ReleaseVersionCalculator.CreateGitHubOutputs(result)).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(options.GitHubSummary))
        {
            await File.AppendAllLinesAsync(options.GitHubSummary, ReleaseVersionCalculator.CreateSummary(result)).ConfigureAwait(false);
        }

        await output.WriteLineAsync(result.VersionJson).ConfigureAwait(false);
        return result.VersionOutput;
    }
}
