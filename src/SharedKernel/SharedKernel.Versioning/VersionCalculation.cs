namespace SharedKernel.Versioning;

/// <summary>
/// Calculates release versions from Conventional Commit history.
/// </summary>
public static class VersionCalculation
{
    /// <summary>
    /// Calculates a version output from the base version and commit messages.
    /// </summary>
    /// <param name="baseVersion">The base released version.</param>
    /// <param name="commitMessages">The commit messages after the base version.</param>
    /// <param name="prerelease">The optional prerelease label.</param>
    /// <param name="sha">The optional source SHA.</param>
    /// <returns>The calculated version output.</returns>
    public static VersionOutput Calculate(
        SemanticVersion baseVersion,
        IEnumerable<string> commitMessages,
        string? prerelease = null,
        string? sha = null)
    {
        ArgumentNullException.ThrowIfNull(baseVersion);
        ArgumentNullException.ThrowIfNull(commitMessages);

        var impact = ReleaseImpact.None;
        foreach (var message in commitMessages)
        {
            if (ConventionalCommitParser.TryParse(message, out var commit) && commit is not null && commit.Impact > impact)
            {
                impact = commit.Impact;
            }
        }

        var semanticVersion = baseVersion.Bump(impact) with { Prerelease = prerelease };
        return VersionOutput.Create(semanticVersion, impact, sha);
    }
}
