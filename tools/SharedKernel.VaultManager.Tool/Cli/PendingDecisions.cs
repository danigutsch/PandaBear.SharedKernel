using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>Fields and findings that need a decision before a merge writes the merged vault. Holds no values.</summary>
/// <param name="Fields">Fields that neither the policy nor a decision resolved.</param>
/// <param name="Findings">Proposed findings that are neither accepted nor rejected, per item, as pending entries.</param>
internal sealed record PendingDecisions(IReadOnlyList<UnresolvedConflict> Fields, IReadOnlyList<ItemFindingDecisions> Findings)
{
    /// <summary>Nothing needs a decision.</summary>
    public static PendingDecisions None { get; } = new([], []);

    /// <summary>Number of findings that need a decision.</summary>
    public int FindingCount => Findings.Sum(item => item.Choices.Count);

    /// <summary>Whether nothing needs a decision.</summary>
    public bool IsEmpty => Fields.Count == 0 && FindingCount == 0;

    /// <summary>
    /// Finds what a pipeline result needs decided: its unresolved fields, and the findings that are not pre-accepted and
    /// have no accept or reject decision.
    /// </summary>
    /// <param name="result">Pipeline result.</param>
    /// <param name="decisions">Decisions the pipeline ran with.</param>
    /// <returns>The pending fields and findings.</returns>
    public static PendingDecisions Find(PipelineResult result, MergeDecisions decisions)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(decisions);

        ItemFindingDecisions[] findings =
        [
            .. result.Findings
                .Select(item => new ItemFindingDecisions(
                    item.OriginKey,
                    [
                        .. item.Findings
                            .Where(finding => !finding.IsPreAccepted && decisions.FindFinding(item.OriginKey, finding.Id) is null)
                            .Select(finding => new FindingChoice(finding.Id, FindingDecision.Pending)),
                    ],
                    item.ItemName))
                .Where(item => item.Choices.Count > 0),
        ];
        return new PendingDecisions(result.Unresolved, findings);
    }
}
