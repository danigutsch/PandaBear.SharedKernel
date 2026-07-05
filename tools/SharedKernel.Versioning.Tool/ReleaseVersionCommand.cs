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

        var baseVersion = string.IsNullOrWhiteSpace(sourceTag) ? "0.1.0" : sourceTag[1..];
        var commitRange = string.IsNullOrWhiteSpace(sourceTag) ? "HEAD" : sourceTag + "..HEAD";
        var logOutput = CommandRunner.Run("git", ["log", "--format=%B%x00", commitRange], options.RepoRoot);
        var messages = await CommitMessageInput.ReadMessages(new StringReader(logOutput)).ConfigureAwait(false);
        var prerelease = options.VersionKind == "stable" || string.IsNullOrWhiteSpace(options.RunNumber)
            ? null
            : "alpha." + options.RunNumber;
        var versionOutput = VersionCalculation.Calculate(SemanticVersion.Parse(baseVersion), messages, prerelease, sourceSha);

        var versionJson = VersionOutputJson.Serialize(versionOutput);
        if (!string.IsNullOrWhiteSpace(options.GitHubOutput))
        {
            await File.AppendAllLinesAsync(
                options.GitHubOutput,
                CreateGitHubOutputs(baseVersion, sourceTag, versionJson, versionOutput)).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(options.GitHubSummary))
        {
            await File.AppendAllLinesAsync(options.GitHubSummary, CreateSummary(baseVersion, sourceTag, versionJson)).ConfigureAwait(false);
        }

        await output.WriteLineAsync(versionJson).ConfigureAwait(false);
        return versionOutput;
    }

    private static string[] CreateGitHubOutputs(string baseVersion, string sourceTag, string versionJson, VersionOutput output) =>
    [
        "base_version=" + baseVersion,
        "source_tag=" + sourceTag,
        "version_json=" + versionJson,
        "sem_ver=" + output.SemVer,
        "release_impact=" + ReleaseImpactText.ToOutputValue(output.ReleaseImpact),
        "package_version=" + output.PackageVersion,
        "assembly_version=" + output.AssemblyVersion,
        "file_version=" + output.FileVersion,
        "informational_version=" + output.InformationalVersion,
    ];

    private static string[] CreateSummary(string baseVersion, string sourceTag, string versionJson) =>
    [
        "## Version calculation",
        string.Empty,
        "- Base version: `" + baseVersion + "`",
        string.IsNullOrWhiteSpace(sourceTag) ? "- Source tag: none" : "- Source tag: `" + sourceTag + "`",
        "- Output: `" + versionJson + "`",
    ];
}
