namespace PandaBear.Versioning.Tool;

/// <summary>Arguments of an API compatibility run.</summary>
/// <param name="Version">Package version to pack.</param>
/// <param name="OutputRoot">Directory that receives the report and packages.</param>
/// <param name="ReleasePhase">Release phase, which decides whether compatibility failures only appear in the report.</param>
/// <param name="RepoRoot">Repository root.</param>
/// <param name="BaselineVersion">Published version to compare against, or null for none.</param>
/// <param name="BreakingMarker">Whether the commits since the last release carry a breaking-change marker.</param>
internal sealed record ApiCompatibilityOptions(
    string Version,
    string OutputRoot,
    string ReleasePhase,
    string RepoRoot,
    string? BaselineVersion,
    bool BreakingMarker);
