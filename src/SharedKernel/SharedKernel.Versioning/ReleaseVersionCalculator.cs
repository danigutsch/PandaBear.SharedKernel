namespace PandaBear.Versioning;

/// <summary>
/// Calculates release versions and automation output from Git-derived inputs.
/// </summary>
public static class ReleaseVersionCalculator
{
    /// <summary>
    /// Gets the commit range to read for a source tag.
    /// </summary>
    /// <param name="sourceTag">The source tag.</param>
    /// <returns>The Git commit range.</returns>
    public static string GetCommitRange(string sourceTag) => string.IsNullOrWhiteSpace(sourceTag) ? "HEAD" : sourceTag + "..HEAD";

    /// <summary>
    /// Calculates release version output from Git history data.
    /// </summary>
    /// <param name="input">The calculation input.</param>
    /// <returns>The calculation result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
    public static async Task<ReleaseVersionCalculationResult> Calculate(ReleaseVersionCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var baseVersion = GetBaseVersion(input.SourceTag);
        var messages = await CommitMessageInput.ReadMessages(new StringReader(input.LogOutput)).ConfigureAwait(false);
        var prerelease = input.VersionKind == "stable" || string.IsNullOrWhiteSpace(input.RunNumber)
            ? null
            : "alpha." + input.RunNumber;
        var parsedBaseVersion = SemanticVersion.Parse(baseVersion);
        var versionOutput = VersionCalculation.Calculate(parsedBaseVersion, messages, prerelease, input.SourceSha);

        // A prerelease tag, or the initial development version without tags, fixes the target version;
        // prereleases advance only the prerelease identifier and the stable release keeps that core.
        if (string.IsNullOrWhiteSpace(input.SourceTag) || parsedBaseVersion.Prerelease is not null)
        {
            versionOutput = VersionOutput.Create(
                parsedBaseVersion.StableCore with
                {
                    Prerelease = prerelease
                },
                versionOutput.ReleaseImpact,
                input.SourceSha);
        }

        return new ReleaseVersionCalculationResult(
            baseVersion,
            input.SourceTag,
            VersionOutputJson.Serialize(versionOutput),
            versionOutput);
    }

    /// <summary>
    /// Creates GitHub Actions output lines for a release version result.
    /// </summary>
    /// <param name="result">The calculation result.</param>
    /// <returns>The GitHub output lines.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is <see langword="null"/>.</exception>
    public static string[] CreateGitHubOutputs(ReleaseVersionCalculationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return
        [
            "base_version=" + result.BaseVersion,
            "source_tag=" + result.SourceTag,
            "version_json=" + result.VersionJson,
            "sem_ver=" + result.VersionOutput.SemVer,
            "release_impact=" + ReleaseImpactText.ToOutputValue(result.VersionOutput.ReleaseImpact),
            "package_version=" + result.VersionOutput.PackageVersion,
            "assembly_version=" + result.VersionOutput.AssemblyVersion,
            "file_version=" + result.VersionOutput.FileVersion,
            "informational_version=" + result.VersionOutput.InformationalVersion,
        ];
    }

    /// <summary>
    /// Creates GitHub step summary lines for a release version result.
    /// </summary>
    /// <param name="result">The calculation result.</param>
    /// <returns>The summary lines.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is <see langword="null"/>.</exception>
    public static string[] CreateSummary(ReleaseVersionCalculationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return
        [
            "## Version calculation",
            string.Empty,
            "- Base version: `" + result.BaseVersion + "`",
            string.IsNullOrWhiteSpace(result.SourceTag) ? "- Source tag: none" : "- Source tag: `" + result.SourceTag + "`",
            "- Output: `" + result.VersionJson + "`",
        ];
    }

    private static string GetBaseVersion(string sourceTag) => string.IsNullOrWhiteSpace(sourceTag) ? "0.1.0" : sourceTag[1..];
}
