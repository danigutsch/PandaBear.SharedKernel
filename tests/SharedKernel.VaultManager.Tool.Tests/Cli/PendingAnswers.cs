using PandaBear.VaultManager.Tool.Merge;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Answers the pending entries of a decisions file the way a user editing it would.</summary>
internal static class PendingAnswers
{
    /// <summary>Accepts every pending finding and takes the first option for every pending field.</summary>
    public static MergeDecisions AcceptAll(MergeDecisions decisions) => decisions with
    {
        Groups =
        [
            .. decisions.Groups.Select(group => group with
            {
                Choices = [.. group.Choices.Select(choice => choice.Kind == FieldChoiceKind.Pending ? FieldChoice.Member(choice.FieldPath, choice.Options.ShouldNotBeNull()[0].OriginKey) : choice)],
            }),
        ],
        Findings =
        [
            .. decisions.Findings.Select(item => item with
            {
                Choices = [.. item.Choices.Select(choice => choice.Decision == FindingDecision.Pending ? choice with { Decision = FindingDecision.Accepted } : choice)],
            }),
        ],
    };
}
