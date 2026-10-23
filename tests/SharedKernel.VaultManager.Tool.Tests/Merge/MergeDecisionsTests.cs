using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Merge;

public sealed class MergeDecisionsTests
{
    private const string OriginKey = "keeper|keeper.json|rec-1";

    [Fact]
    public void Applies_pre_accepted_finding_unless_the_decisions_reject_it()
    {
        var finding = FindingSamples.Finding(Confidence.High);
        var rejecting = FindingSamples.Decide(OriginKey, finding, FindingDecision.Rejected);

        bool withoutDecision = MergeDecisions.None.Applies(OriginKey, finding);
        bool rejected = rejecting.Applies(OriginKey, finding);

        withoutDecision.ShouldBeTrue();
        rejected.ShouldBeFalse();
    }

    [Fact]
    public void Applies_proposed_finding_only_when_the_decisions_accept_it_for_that_item()
    {
        var finding = FindingSamples.Finding(Confidence.Medium);
        var accepting = FindingSamples.Decide(OriginKey, finding, FindingDecision.Accepted);

        bool withoutDecision = MergeDecisions.None.Applies(OriginKey, finding);
        bool accepted = accepting.Applies(OriginKey, finding);
        bool otherItem = accepting.Applies("keeper|keeper.json|rec-2", finding);

        withoutDecision.ShouldBeFalse();
        accepted.ShouldBeTrue();
        otherItem.ShouldBeFalse();
    }
}
