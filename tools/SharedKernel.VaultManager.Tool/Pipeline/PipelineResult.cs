using System.Text;
using PandaBear.VaultManager.Tool.Analysis;
using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>
/// Everything one pipeline run produced. Reports, problems, and warnings hold names, keys, paths, and counts, never
/// values; only <see cref="Merged"/> and <see cref="Match"/> hold vault items.
/// </summary>
internal sealed record PipelineResult
{
    /// <summary>Merged vault: one item per match group, in group order.</summary>
    public required VaultExport Merged { get; init; }

    /// <summary>Match groups, aligned with <see cref="Merged"/>, and the weak matches that were only suggested.</summary>
    public required MatchResult Match { get; init; }

    /// <summary>Fields that need a user decision.</summary>
    public required IReadOnlyList<UnresolvedConflict> Unresolved { get; init; }

    /// <summary>
    /// Findings that are not pre-accepted and have no accept or reject decision, per input item, as pending entries for
    /// <see cref="MergeDecisions.WithPending"/>. Pre-accepted findings apply without a decision and are not listed.
    /// </summary>
    public IReadOnlyList<ItemFindingDecisions> PendingFindings { get; init; } = [];

    /// <summary>Value-free merge summary.</summary>
    public required MergeReport MergeReport { get; init; }

    /// <summary>Secret findings of every input item that has any.</summary>
    public required IReadOnlyList<ItemFindingsResult> Findings { get; init; }

    /// <summary>Problems placement reported for accepted findings.</summary>
    public required IReadOnlyList<PlacementProblem> PlacementProblems { get; init; }

    /// <summary>Reused, short, or easily guessed passwords of the merged vault.</summary>
    public required PasswordHealthReport PasswordHealth { get; init; }

    /// <summary>
    /// Attachments the user uploads after the import: every attachment of every item, except that a file found on several
    /// merged items with the same name and content is listed once, from the newest item.
    /// </summary>
    public required IReadOnlyList<ManualAttachment> ManualAttachments { get; init; }

    /// <summary>
    /// Attachment folders of Bitwarden zip inputs whose files belong to no item, because the folder matches no item or
    /// several items with the same name. The user uploads their files by hand.
    /// </summary>
    public IReadOnlyList<UnassignedAttachmentFolder> UnassignedAttachmentFolders { get; init; } = [];

    /// <summary>Problems that make Bitwarden or Vaultwarden reject the whole import. Any of them blocks writing.</summary>
    public required IReadOnlyList<string> WriteErrors { get; init; }

    /// <summary>Adjustments the writer made, or risks that do not block the import.</summary>
    public required IReadOnlyList<string> WriteWarnings { get; init; }

    /// <summary>Warnings raised while reading the inputs and scanning their attachments.</summary>
    public required IReadOnlyList<string> ReadWarnings { get; init; }

    /// <summary>Requested output, or <see langword="null"/> when none was requested.</summary>
    public PipelineOutput? Output { get; init; }

    /// <summary>Whether the merged vault was written to <see cref="Output"/>.</summary>
    public bool OutputWritten { get; init; }

    /// <summary>Whether a field still needs a decision. The merged vault is then not written.</summary>
    public bool HasUnresolvedConflicts => Unresolved.Count > 0;

    /// <summary>Number of findings in <see cref="PendingFindings"/>. While it is not 0, the merged vault is not written.</summary>
    public int PendingFindingCount => PendingFindings.Sum(item => item.Choices.Count);

    /// <summary>Whether the writer reported an error. The merged vault is then not written.</summary>
    public bool HasWriteErrors => WriteErrors.Count > 0;

    /// <summary>Prints counts and flags only, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Items = ").Append(Merged.Items.Count)
            .Append(", Unresolved = ").Append(Unresolved.Count)
            .Append(", Findings = ").Append(Findings.Sum(item => item.Findings.Count))
            .Append(", PendingFindings = ").Append(PendingFindingCount)
            .Append(", PlacementProblems = ").Append(PlacementProblems.Count)
            .Append(", ManualAttachments = ").Append(ManualAttachments.Count)
            .Append(", UnassignedAttachmentFolders = ").Append(UnassignedAttachmentFolders.Count)
            .Append(", WriteErrors = ").Append(WriteErrors.Count)
            .Append(", OutputWritten = ").Append(OutputWritten);
        return true;
    }
}
