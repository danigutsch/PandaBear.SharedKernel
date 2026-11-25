using SharedKernel.Vault.Matching;
using SharedKernel.Vault.Merge;
using SharedKernel.Vault.Pipeline;

namespace SharedKernel.Vault.Tests.Review;

/// <summary>Tests that hosts can report stale decisions while resuming a saved review.</summary>
public sealed class UnmatchedDecisionsTests
{
    [Fact]
    public void A_review_host_can_find_stale_overrides_without_treating_pending_entries_as_decisions()
    {
        // Arrange
        const string staleOrigin = "missing-item";
        var pipeline = ReviewCheckpointFixtures.CreatePipeline();
        var result = pipeline.Merge(ReviewCheckpointFixtures.ConflictingLogins(), new MergeRequest(), TestContext.Current.CancellationToken);
        string group = result.Match.Groups.ShouldHaveSingleItem().Key;
        var decisions = new MergeDecisions(
            [
                new GroupDecision("missing-group", [FieldChoice.Empty(FieldPaths.LoginPassword)]),
                new GroupDecision("pending-group", [FieldChoice.Pending(FieldPaths.LoginPassword, [])]),
                new GroupDecision(group, [FieldChoice.Empty("login.missing-field")]),
            ],
            new MatchOverrides([new OriginKeyPair(ReviewCheckpointFixtures.KeeperKey, staleOrigin)], []))
        {
            Findings =
            [
                new ItemFindingDecisions(staleOrigin, [new FindingChoice("missing-finding", FindingDecision.Rejected)]),
                new ItemFindingDecisions("pending-item", [new FindingChoice("pending-finding", FindingDecision.Pending)]),
            ],
        };

        // Act
        IReadOnlyList<string> warnings = UnmatchedDecisions.Find(decisions, result);

        // Assert
        warnings.ShouldBe(
        [
            "decisions for group missing-group match no merged group and are ignored.",
            $"the decision on field login.missing-field of group {group} matches no field that takes Empty decisions and is ignored.",
            $"the link between {ReviewCheckpointFixtures.KeeperKey} and {staleOrigin} names {staleOrigin}, which matches no item, and is ignored.",
            $"finding decisions for {staleOrigin} match no item with findings and are ignored.",
        ]);
    }
}
