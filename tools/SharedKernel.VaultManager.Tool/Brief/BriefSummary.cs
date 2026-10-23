namespace PandaBear.VaultManager.Tool.Brief;

/// <summary>Counts of a merge, as the merge summary on standard output gives them.</summary>
/// <param name="Items">Items in the merged vault.</param>
/// <param name="MergedGroups">Groups with more than one member.</param>
/// <param name="ConflictsResolvedByPolicy">Conflicts the policy resolved without a decision.</param>
/// <param name="FieldsNeedingDecision">Fields that need a decision.</param>
/// <param name="Findings">Secret findings of every input item.</param>
/// <param name="FindingsApplied">Findings that are moved to their proposed place.</param>
/// <param name="FindingsNeedingDecision">Findings that need an accept or reject decision.</param>
/// <param name="PasswordIssues">Reused or weak passwords of the merged vault.</param>
/// <param name="AttachmentsToUpload">Files to upload by hand after the import.</param>
internal sealed record BriefSummary(
    int Items,
    int MergedGroups,
    int ConflictsResolvedByPolicy,
    int FieldsNeedingDecision,
    int Findings,
    int FindingsApplied,
    int FindingsNeedingDecision,
    int PasswordIssues,
    int AttachmentsToUpload);
