using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

/// <summary>Builds decisions on the findings of one item.</summary>
internal static class PipelineDecisions
{
    public static MergeDecisions Findings(string originKey, params FindingChoice[] choices) =>
        new([], MatchOverrides.None)
        {
            Findings = [new ItemFindingDecisions(originKey, choices)]
        };
}
