namespace PandaBear.Versioning.Tool;

internal sealed record ApiCompatibilityOptions(
    string Version,
    string OutputRoot,
    string ReleasePhase,
    string RepoRoot,
    string? BaselineVersion,
    bool BreakingMarker);
