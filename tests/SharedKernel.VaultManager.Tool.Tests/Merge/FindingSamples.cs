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

    /// <summary>A medium-confidence finding at the start of a field.</summary>
    public static SecretFinding At(string fieldPath) => Finding(Confidence.Medium) with { Location = new SecretLocation(fieldPath, 0, 4) };

    public static MergeDecisions Decide(string originKey, SecretFinding finding, FindingDecision decision) =>
        new([], MatchOverrides.None)
        {
            Findings = [new ItemFindingDecisions(originKey, [new FindingChoice(finding.Id, decision)])]
        };
}
