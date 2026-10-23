using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using static PandaBear.VaultManager.Tool.Tests.Merge.MergeFixture;

namespace PandaBear.VaultManager.Tool.Tests.Merge;

public sealed class DecisionsFileTests
{
    private const string OldPassword = "fixture-old-password";
    private const string NewPassword = "fixture-new-password";

    [Fact]
    public void Decisions_round_trip_and_contain_no_values()
    {
        var keeper = Keeper(Login(OldPassword)) with { Notes = "fixture-old-notes" };
        var bitwarden = Bitwarden(Login(NewPassword)) with { Notes = "fixture-new-notes" };
        var unresolved = Run(new MergePolicy(MergePolicyKind.Ask), keeper, bitwarden).Unresolved;
        var decisions = new MergeDecisions(
            [new GroupDecision(unresolved[0].GroupKey, [.. unresolved.Select(conflict => FieldChoice.Member(conflict.FieldPath, conflict.MemberOriginKeys[0])), FieldChoice.Empty(FieldPaths.LoginTotp)])],
            new MatchOverrides([new OriginKeyPair(KeeperKey, BitwardenKey)], [new OriginKeyPair("a|b|c", "d|e|f")]));

        string json = DecisionsFile.Serialize(decisions);
        var reread = DecisionsFile.Deserialize(json);

        json.ShouldContain("\"version\": 1", StringComparison.Ordinal);
        string[] values = [OldPassword, NewPassword, "fixture-old-notes", "fixture-new-notes"];
        values.ShouldAllSatisfy(value => json.ShouldNotContain(value, StringComparison.Ordinal));
        var group = reread.Groups.ShouldHaveSingleItem();
        group.GroupKey.ShouldBe(unresolved[0].GroupKey);
        group.Choices.ShouldBe(decisions.Groups[0].Choices);
        reread.Overrides.Links.ShouldBe(decisions.Overrides.Links);
        reread.Overrides.Splits.ShouldBe(decisions.Overrides.Splits);
    }

    [Fact]
    public void Decisions_with_finding_decisions_round_trip_without_values()
    {
        const string NotesPassword = "fixture-notes-password";
        var decisions = new MergeDecisions([], MatchOverrides.None)
        {
            Findings =
            [
                new ItemFindingDecisions(KeeperKey, [new FindingChoice("notes-password@notes:42", FindingDecision.Accepted), new FindingChoice("recovery-code-block@notes:0", FindingDecision.Rejected)]),
                new ItemFindingDecisions(BitwardenKey, [new FindingChoice("card-number@fields[0].value:0", FindingDecision.Rejected)]),
            ],
        };

        string json = DecisionsFile.Serialize(decisions);
        var reread = DecisionsFile.Deserialize(json);

        json.ShouldContain("\"version\": 1", StringComparison.Ordinal);
        json.ShouldContain("\"findings\"", StringComparison.Ordinal);
        json.ShouldNotContain(NotesPassword, StringComparison.Ordinal);
        reread.Findings.Count.ShouldBe(2);
        reread.Findings[0].OriginKey.ShouldBe(KeeperKey);
        reread.Findings[0].Choices.ShouldBe(decisions.Findings[0].Choices);
        reread.Findings[1].Choices.ShouldBe(decisions.Findings[1].Choices);
        reread.FindFinding(KeeperKey, "notes-password@notes:42").ShouldBe(FindingDecision.Accepted);
        reread.FindFinding(BitwardenKey, "card-number@fields[0].value:0").ShouldBe(FindingDecision.Rejected);
        reread.FindFinding(BitwardenKey, "notes-password@notes:42").ShouldBeNull();
    }

    [Fact]
    public void Decisions_written_without_a_findings_section_still_load()
    {
        var decisions = DecisionsFile.Deserialize("""{"version":1,"groups":[],"links":[],"splits":[]}""");

        decisions.Findings.ShouldBeEmpty();
    }

    [Fact]
    public void Decisions_edited_by_hand_with_lowercase_finding_decisions_load()
    {
        var decisions = DecisionsFile.Deserialize("""{"version":1,"groups":[],"links":[],"splits":[],"findings":[{"originKey":"k","choices":[{"findingId":"f","decision":"rejected"}]}]}""");

        decisions.FindFinding("k", "f").ShouldBe(FindingDecision.Rejected);
    }

    [Theory]
    [InlineData("""{"version":1,"groups":[],"links":[],"splits":[],"findings":[{"originKey":" ","choices":[]}]}""")]
    [InlineData("""{"version":1,"groups":[],"links":[],"splits":[],"findings":[{"originKey":"k","choices":[{"findingId":"","decision":"accepted"}]}]}""")]
    [InlineData("""{"version":1,"groups":[],"links":[],"splits":[],"findings":[{"originKey":"k","choices":[{"findingId":"f","decision":7}]}]}""")]
    [InlineData("""{"version":1,"groups":[],"links":[],"splits":[],"findings":[{"originKey":"k","choices":[{"findingId":"f"}]}]}""")]
    [InlineData("""{"version":1,"groups":[],"links":[],"splits":[],"findings":[{"originKey":"k","choices":null}]}""")]
    public void Decisions_with_incomplete_finding_decisions_are_rejected(string json)
    {
        var action = () => DecisionsFile.Deserialize(json);

        action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void Decisions_with_unsupported_version_are_rejected()
    {
        var action = () => DecisionsFile.Deserialize("""{"version":2,"groups":[],"links":[],"splits":[]}""");

        action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void Decisions_with_member_choice_without_origin_key_are_rejected()
    {
        var action = () => DecisionsFile.Deserialize("""{"version":1,"groups":[{"groupKey":"g","choices":[{"fieldPath":"notes","kind":"member"}]}],"links":[],"splits":[]}""");

        action.ShouldThrow<InvalidDataException>();
    }
}
