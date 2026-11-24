namespace PandaBear.Repositories;

/// <summary>Records the comparison and completeness of one relative project path.</summary>
/// <param name="Project">The repository-relative project path.</param>
/// <param name="BaselineLockExempt">Whether the baseline project explicitly disables lock files, or null when absent.</param>
/// <param name="CandidateLockExempt">Whether the candidate project explicitly disables lock files, or null when absent.</param>
/// <param name="BaselineUnavailable">Why restore evidence is unavailable for the baseline project.</param>
/// <param name="CandidateUnavailable">Why restore evidence is unavailable for the candidate project.</param>
/// <param name="Changes">The package differences when both sides can be compared.</param>
internal sealed record NuGetRestoreProjectReport(
    string Project,
    bool? BaselineLockExempt,
    bool? CandidateLockExempt,
    string? BaselineUnavailable,
    string? CandidateUnavailable,
    IReadOnlyList<NuGetRestoreChange> Changes);
