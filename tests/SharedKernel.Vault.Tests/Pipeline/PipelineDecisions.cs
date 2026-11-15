using PandaBear.Vault.Matching;
using PandaBear.Vault.Merge;

namespace PandaBear.Vault.Tests.Pipeline;

/// <summary>Builds decisions on the findings of one item.</summary>
internal static class PipelineDecisions
{
    /// <summary>Creates merge decisions with no group decisions or overrides and the given finding choices for the item.</summary>
    public static MergeDecisions Findings(string originKey, params FindingChoice[] choices) =>
        new([], MatchOverrides.None)
        {
            Findings = [new ItemFindingDecisions(originKey, choices)]
        };
}
