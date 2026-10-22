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
