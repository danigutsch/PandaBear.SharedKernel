using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Merge;

/// <summary>Tests how <see cref="MergeDecisions"/> applies finding decisions, finds field choices, and rebuilds pending entries.</summary>
public sealed class MergeDecisionsTests
{
    private const string OriginKey = "keeper|keeper.json|rec-1";
    private const string OtherOriginKey = "bitwarden|bitwarden.json|bw-1";
    private const string GroupKey = "0000000000000000";

    [Fact]
    public void Applies_pre_accepted_finding_unless_the_decisions_reject_it()
    {
        // Arrange
        var finding = FindingSamples.Finding(Confidence.High);
        var rejecting = FindingSamples.Decide(OriginKey, finding, FindingDecision.Rejected);

        // Act
        bool withoutDecision = MergeDecisions.None.Applies(OriginKey, finding);
        bool rejected = rejecting.Applies(OriginKey, finding);

        // Assert
        withoutDecision.ShouldBeTrue();
        rejected.ShouldBeFalse();
    }

    [Fact]
    public void Applies_proposed_finding_only_when_the_decisions_accept_it_for_that_item()
    {
        // Arrange
        var finding = FindingSamples.Finding(Confidence.Medium);
        var accepting = FindingSamples.Decide(OriginKey, finding, FindingDecision.Accepted);

        // Act
        bool withoutDecision = MergeDecisions.None.Applies(OriginKey, finding);
        bool accepted = accepting.Applies(OriginKey, finding);
        bool otherItem = accepting.Applies("keeper|keeper.json|rec-2", finding);

        // Assert
        withoutDecision.ShouldBeFalse();
        accepted.ShouldBeTrue();
        otherItem.ShouldBeFalse();
    }

    [Fact]
    public void Default_finding_decision_does_not_apply_a_proposed_finding()
    {
        // Arrange
        var finding = FindingSamples.Finding(Confidence.Medium);
        var decisions = FindingSamples.Decide(OriginKey, finding, default);

        // Act
        bool applies = decisions.Applies(OriginKey, finding);

        // Assert
        applies.ShouldBeFalse();
    }

    [Fact]
    public void Pending_finding_decision_counts_as_no_decision()
    {
        // Arrange
        var proposed = FindingSamples.Finding(Confidence.Medium);
        var preAccepted = FindingSamples.Finding(Confidence.High) with
        {
            RuleId = "fixture-pre-accepted"
        };
        var decisions = new MergeDecisions([], MatchOverrides.None)
        {
            Findings = [new ItemFindingDecisions(OriginKey, [new FindingChoice(proposed.Id, FindingDecision.Pending), new FindingChoice(preAccepted.Id, FindingDecision.Pending)])],
        };

        // Act
        var proposedDecision = decisions.FindFinding(OriginKey, proposed.Id);
        bool proposedApplies = decisions.Applies(OriginKey, proposed);
        bool preAcceptedApplies = decisions.Applies(OriginKey, preAccepted);

        // Assert
        proposedDecision.ShouldBeNull();
        proposedApplies.ShouldBeFalse();
        preAcceptedApplies.ShouldBeTrue();
    }

    [Fact]
    public void Pending_field_choice_counts_as_no_decision_and_a_later_decision_still_counts()
    {
        // Arrange
        var member = FieldChoice.Member(FieldPaths.LoginPassword, OtherOriginKey);
        var decisions = new MergeDecisions(
            [
                new GroupDecision(GroupKey, [FieldChoice.Pending(FieldPaths.Notes, [new FieldChoiceOption(OriginKey, "Example")])]),
                new GroupDecision(GroupKey, [FieldChoice.Pending(FieldPaths.LoginPassword, []), member]),
            ],
            MatchOverrides.None);

        // Act
        var notes = decisions.Find(GroupKey, FieldPaths.Notes);
        var password = decisions.Find(GroupKey, FieldPaths.LoginPassword);

        // Assert
        notes.ShouldBeNull();
        password.ShouldBe(member);
    }

    [Fact]
    public void Pending_entries_are_rebuilt_while_decisions_and_overrides_are_kept()
    {
        // Arrange
        var link = new OriginKeyPair(OriginKey, OtherOriginKey);
        var decided = FieldChoice.Member(FieldPaths.LoginPassword, OtherOriginKey);
        var existing = new MergeDecisions(
            [new GroupDecision(GroupKey, [decided, FieldChoice.Pending(FieldPaths.Folder, [])])],
            new MatchOverrides([link], []))
        {
            Findings =
            [
                new ItemFindingDecisions(OriginKey, [new FindingChoice("rejected@notes:0", FindingDecision.Rejected), new FindingChoice("stale@notes:9", FindingDecision.Pending)]),
            ],
        };
        string[] members = [OriginKey, OtherOriginKey];
        string[] names = ["Example", "Example (Bitwarden)"];
        UnresolvedConflict[] fields =
        [
            new(GroupKey, FieldPaths.LoginPassword, members, names),
            new(GroupKey, FieldPaths.Notes, members, names),
            new("fedcba9876543210", FieldPaths.LoginUsername, members, names),
        ];
        ItemFindingDecisions[] findings =
        [
            new(OriginKey, [new FindingChoice("rejected@notes:0", FindingDecision.Pending), new FindingChoice("proposed@notes:20", FindingDecision.Pending)], "Example"),
            new(OtherOriginKey, [new FindingChoice("proposed@fields[0].value:0", FindingDecision.Pending)], "Example (Bitwarden)"),
        ];

        // Act
        var updated = existing.WithPending(fields, findings);

        // Assert
        updated.Overrides.Links.ShouldBe([link]);
        updated.Groups.Count.ShouldBe(2);
        updated.Groups[0].GroupKey.ShouldBe(GroupKey);
        updated.Groups[0].Choices.Select(choice => (choice.FieldPath, choice.Kind)).ShouldBe([(FieldPaths.LoginPassword, FieldChoiceKind.Member), (FieldPaths.Notes, FieldChoiceKind.Pending)]);
        updated.Groups[0].Choices[0].ShouldBe(decided);
        var notesOptions = updated.Groups[0].Choices[1].Options.ShouldNotBeNull();
        notesOptions.ShouldBe([new FieldChoiceOption(OriginKey, "Example"), new FieldChoiceOption(OtherOriginKey, "Example (Bitwarden)")]);
        updated.Groups[1].GroupKey.ShouldBe("fedcba9876543210");
        updated.Groups[1].Choices.ShouldHaveSingleItem().FieldPath.ShouldBe(FieldPaths.LoginUsername);
        updated.Findings.Count.ShouldBe(2);
        updated.Findings[0].OriginKey.ShouldBe(OriginKey);
        updated.Findings[0].ItemName.ShouldBe("Example");
        updated.Findings[0].Choices.ShouldBe([new FindingChoice("rejected@notes:0", FindingDecision.Rejected), new FindingChoice("proposed@notes:20", FindingDecision.Pending)]);
        updated.Findings[1].OriginKey.ShouldBe(OtherOriginKey);
        updated.Findings[1].ItemName.ShouldBe("Example (Bitwarden)");
        updated.Findings[1].Choices.ShouldBe([new FindingChoice("proposed@fields[0].value:0", FindingDecision.Pending)]);
    }

    [Fact]
    public void Member_choice_naming_an_item_outside_its_group_is_no_decision_and_its_pending_entry_takes_its_place()
    {
        // Arrange
        var stale = FieldChoice.Member(FieldPaths.LoginPassword, "keeper|keeper.json|rec-gone");
        var empty = FieldChoice.Empty(FieldPaths.Notes);
        var existing = new MergeDecisions([new GroupDecision(GroupKey, [stale, empty])], MatchOverrides.None);
        string[] members = [OriginKey, OtherOriginKey];
        UnresolvedConflict[] fields = [new(GroupKey, FieldPaths.LoginPassword, members, ["Example", "Example (Bitwarden)"])];

        // Act
        var updated = existing.WithPending(fields, []);

        // Assert
        var choices = updated.Groups.ShouldHaveSingleItem().Choices;
        choices.Select(choice => (choice.FieldPath, choice.Kind)).ShouldBe([(FieldPaths.LoginPassword, FieldChoiceKind.Pending), (FieldPaths.Notes, FieldChoiceKind.Empty)]);
        choices[0].Options.ShouldNotBeNull().Select(option => option.OriginKey).ShouldBe(members);
        choices[1].ShouldBe(empty);
    }

    [Fact]
    public void Pending_entries_are_dropped_from_groups_and_items_left_without_choices()
    {
        // Arrange
        var existing = new MergeDecisions([new GroupDecision(GroupKey, [FieldChoice.Pending(FieldPaths.Notes, [])])], MatchOverrides.None)
        {
            Findings = [new ItemFindingDecisions(OriginKey, [new FindingChoice("stale@notes:0", FindingDecision.Pending)])],
        };

        // Act
        var updated = existing.WithPending([], []);

        // Assert
        updated.Groups.ShouldBeEmpty();
        updated.Findings.ShouldBeEmpty();
    }
}
