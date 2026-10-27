namespace PandaBear.Versioning.Tool;

/// <summary>Arguments of a release version calculation.</summary>
/// <param name="RepoRoot">Repository root.</param>
/// <param name="VersionKind">Kind of version to calculate, such as prerelease.</param>
/// <param name="RunNumber">CI run number, required for prerelease versions.</param>
/// <param name="Sha">Source commit SHA, or null to use HEAD.</param>
/// <param name="GitHubOutput">File that receives GitHub Actions step outputs, or null.</param>
/// <param name="GitHubSummary">File that receives the GitHub Actions step summary, or null.</param>
internal sealed record CalculateReleaseOptions(
    string RepoRoot,
    string VersionKind,
    string? RunNumber,
    string? Sha,
    string? GitHubOutput,
    string? GitHubSummary);
