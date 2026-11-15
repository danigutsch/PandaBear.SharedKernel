namespace PandaBear.Versioning;

/// <summary>Which repository and version kind <see cref="ReleaseHistory.CalculateVersion"/> calculates for.</summary>
/// <param name="RepoRoot">The Git repository root.</param>
/// <param name="VersionKind"><c>prerelease</c>, which adds <c>alpha.&lt;run number&gt;</c>, or <c>stable</c>.</param>
/// <param name="RunNumber">The CI run number of a prerelease label; required for <c>prerelease</c>.</param>
/// <param name="Sha">The source revision, or <see langword="null"/> for <c>HEAD</c>.</param>
public sealed record ReleaseVersionOptions(string RepoRoot, string VersionKind, string? RunNumber = null, string? Sha = null);
