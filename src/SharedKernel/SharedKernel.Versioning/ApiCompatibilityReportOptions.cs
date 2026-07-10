namespace SharedKernel.Versioning;

/// <summary>
/// Defines API compatibility report inputs.
/// </summary>
/// <param name="Version">The version being checked.</param>
/// <param name="OutputRoot">The report output root.</param>
/// <param name="ReleasePhase">The release phase.</param>
/// <param name="RepoRoot">The repository root.</param>
/// <param name="BaselineVersion">The optional package validation baseline version.</param>
/// <param name="BreakingMarker">Whether the compared range includes a breaking-change marker.</param>
public sealed record ApiCompatibilityReportOptions(
    string Version,
    string OutputRoot,
    string ReleasePhase,
    string RepoRoot,
    string? BaselineVersion,
    bool BreakingMarker);
