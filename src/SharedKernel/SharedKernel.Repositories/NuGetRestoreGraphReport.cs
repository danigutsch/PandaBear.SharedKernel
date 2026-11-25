namespace SharedKernel.Repositories;

/// <summary>The deterministic comparison of two evaluated solution restore graphs.</summary>
/// <param name="SchemaVersion">The report format version.</param>
/// <param name="BaselineRevision">The baseline commit.</param>
/// <param name="BaselineDirty">Whether the baseline checkout has pending changes.</param>
/// <param name="BaselineSdkVersion">The SDK that evaluated the baseline.</param>
/// <param name="BaselineVerification">The optional recorded baseline verification.</param>
/// <param name="CandidateRevision">The candidate commit.</param>
/// <param name="CandidateDirty">Whether the candidate checkout has pending changes.</param>
/// <param name="CandidateSdkVersion">The SDK that evaluated the candidate.</param>
/// <param name="CandidateVerification">The optional recorded candidate verification.</param>
/// <param name="Projects">Per-project completeness and graph differences.</param>
internal sealed record NuGetRestoreGraphReport(
    int SchemaVersion,
    string BaselineRevision,
    bool BaselineDirty,
    string BaselineSdkVersion,
    NuGetRestoreVerification? BaselineVerification,
    string CandidateRevision,
    bool CandidateDirty,
    string CandidateSdkVersion,
    NuGetRestoreVerification? CandidateVerification,
    IReadOnlyList<NuGetRestoreProjectReport> Projects);
