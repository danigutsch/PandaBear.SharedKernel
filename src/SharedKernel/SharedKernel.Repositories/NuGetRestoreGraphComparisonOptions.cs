namespace SharedKernel.Repositories;

/// <summary>Identifies the checkouts and optional CI receipts for a restore graph comparison.</summary>
/// <param name="BaselineRoot">The baseline Git checkout.</param>
/// <param name="CandidateRoot">The candidate checkout, relative to the baseline root if needed.</param>
/// <param name="OutputPath">A new report path inside the baseline checkout.</param>
/// <param name="BaselineVerification">An optional CI execution manifest inside the baseline checkout.</param>
/// <param name="CandidateVerification">An optional CI execution manifest inside the candidate checkout.</param>
public sealed record NuGetRestoreGraphComparisonOptions(
    string BaselineRoot, string CandidateRoot, string OutputPath, string? BaselineVerification, string? CandidateVerification);
