namespace PandaBear.Repositories;

/// <summary>Describes the source snapshot, selected changes and candidate verification.</summary>
/// <param name="SchemaVersion">The trial report schema version.</param>
/// <param name="Revision">Git revision from which the trial began.</param>
/// <param name="Status">Whether the trial verified an upgrade, unchanged inputs, or a blocked attempt.</param>
/// <param name="Reason">A short explanation of the status.</param>
/// <param name="RemovePin">The central declaration selected for removal, if any.</param>
/// <param name="RequestedUpdates">The explicit version changes requested by the caller.</param>
/// <param name="Steps">Verification commands and SDK diagnostics.</param>
/// <param name="BaselinePackages">The projects, targets and requesting paths affected by the trial.</param>
/// <param name="Graph">The per-project and target dependency comparison, when available.</param>
/// <param name="PatchSha256">The SHA-256 of the reviewed patch, when one was produced.</param>
internal sealed record NuGetUpgradeTrialReport(
    int SchemaVersion, string Revision, string Status, string Reason, string? RemovePin,
    IReadOnlyList<string> RequestedUpdates, IReadOnlyList<NuGetTrialStep> Steps, IReadOnlyList<NuGetTrialPackageEvidence> BaselinePackages,
    NuGetRestoreGraphReport? Graph, string? PatchSha256)
{
    /// <summary>Whether the audit search established the minimum and which exact choices it tried.</summary>
    public NuGetAuditMinimumReport? Minimum { get; init; }
}
