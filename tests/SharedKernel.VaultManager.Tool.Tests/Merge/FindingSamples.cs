using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Merge;

/// <summary>Builds secret findings and finding decisions without scanning an item.</summary>
internal static class FindingSamples
{
    public static SecretFinding Finding(Confidence confidence) => new()
    {
        RuleId = "fixture-rule",
        Kind = SecretKind.GenericSecret,
        Confidence = confidence,
        Location = new SecretLocation(SecretLocation.Notes, 0, 8),
        ProposedTarget = PlacementTarget.Hidden("Secret"),
        Message = "Fixture finding",
    };

    public static MergeDecisions Decide(string originKey, SecretFinding finding, FindingDecision decision) =>
        new([], MatchOverrides.None) { Findings = [new ItemFindingDecisions(originKey, [new FindingChoice(finding.Id, decision)])] };
}
