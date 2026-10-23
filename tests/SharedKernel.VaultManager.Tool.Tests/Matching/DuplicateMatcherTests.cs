using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Model;
using static PandaBear.VaultManager.Tool.Tests.Matching.MatchFixture;

namespace PandaBear.VaultManager.Tool.Tests.Matching;

public sealed class DuplicateMatcherTests
{
    [Fact]
    public void Match_same_host_and_username_groups_items()
    {
        var first = Login("a", "Mail", "alice", "https://mail.example.com");
        var second = Login("b", "Webmail", "Alice ", "https://mail.example.com/inbox");

        var result = DuplicateMatcher.Match([first, second]);

        var group = result.Groups.ShouldHaveSingleItem();
        group.Members.ShouldBe([first, second]);
        group.MemberKeys.ShouldBe(["bitwarden|vault.json|a", "bitwarden|vault.json|b"]);
        group.Status.ShouldBe(MatchStatus.Different);
    }

    [Fact]
    public void Match_www_scheme_and_path_are_ignored()
    {
        VaultItem[] items =
        [
            Login("a", "One", "alice", "https://www.Example.com:8443/login?next=1#top"),
            Login("b", "Two", "alice", "example.com/account"),
            Login("c", "Three", "alice", "http://EXAMPLE.COM"),
        ];

        var result = DuplicateMatcher.Match(items);

        result.Groups.ShouldHaveSingleItem().Members.Count.ShouldBe(3);
    }

    [Fact]
    public void Match_same_host_different_username_stays_separate()
    {
        var alice = Login("a", "Mail", "alice", "https://mail.example.com");
        var bob = Login("b", "Mail", "bob", "https://mail.example.com");

        var result = DuplicateMatcher.Match([alice, bob]);

        result.Groups.Count.ShouldBe(2);
        result.Groups.ShouldAllSatisfy(group => group.Status.ShouldBe(MatchStatus.Unique));
    }

    [Fact]
    public void Match_title_and_type_fallback_without_uri()
    {
        var first = Note("n1", "Home  WiFi");
        var second = Note("n2", " home wifi");
        var login = Login("l1", "Home WiFi", null);

        var result = DuplicateMatcher.Match([first, second, login]);

        result.Groups.Count.ShouldBe(2);
        result.Groups[0].Members.ShouldBe([first, second]);
        result.Groups[1].Members.ShouldBe([login]);
        result.Suggestions.ShouldContain(suggestion => suggestion.Kind == MatchSuggestionKind.SameTitle);
    }

    [Fact]
    public void Match_same_host_only_is_suggested_not_grouped()
    {
        var alice = Login("a", "Mail", "alice", "https://mail.example.com");
        var bob = Login("b", "Mail for Bob", "bob", "https://www.mail.example.com");

        var result = DuplicateMatcher.Match([alice, bob]);

        result.Groups.Count.ShouldBe(2);
        var suggestion = result.Suggestions.ShouldHaveSingleItem();
        suggestion.Kind.ShouldBe(MatchSuggestionKind.SameHostDifferentUsername);
        suggestion.FirstOriginKey.ShouldBe("bitwarden|vault.json|a");
        suggestion.SecondOriginKey.ShouldBe("bitwarden|vault.json|b");
        suggestion.FirstName.ShouldBe("Mail");
        suggestion.SecondName.ShouldBe("Mail for Bob");
    }

    [Fact]
    public void Match_group_keys_are_stable_across_runs()
    {
        VaultItem[] items =
        [
            Login("a", "Mail", "alice", "https://mail.example.com"),
            Login("b", "Mail", "alice", "https://mail.example.com"),
            Note(null, "Without id"),
            Note(null, "Without id"),
            Note(null, "Other"),
        ];

        var first = DuplicateMatcher.Match(items).Groups.Select(group => group.Key).ToArray();
        var second = DuplicateMatcher.Match([.. items.Select(item => item with { })]).Groups.Select(group => group.Key).ToArray();
        var reversed = DuplicateMatcher.Match([items[1], items[0], .. items[2..]]).Groups.Select(group => group.Key).ToArray();

        second.ShouldBe(first);
        reversed.ShouldBe(first);
        first.Length.ShouldBe(3);
        first.ShouldAllSatisfy(key => key.ShouldMatch("^[0-9a-f]{16}$"));
        string[] contentKeys = [.. OriginKeys.Compute(items).Skip(2)];
        contentKeys.ShouldAllSatisfy(key => key.ShouldStartWith("keeper|keeper.json|content:", StringComparison.Ordinal));
        contentKeys.Distinct(StringComparer.Ordinal).Count().ShouldBe(3);
    }

    [Fact]
    public void Match_with_supplied_origin_keys_uses_them_for_groups_overrides_and_suggestions()
    {
        var first = Login("a", "Mail", "alice", "https://mail.example.com");
        var second = Login("b", "Mail", "alice", "https://mail.example.com");
        var other = Login("c", "Mail", "bob", "https://mail.example.com");
        string[] keys = ["input|one", "input|two", "input|three"];
        var overrides = new MatchOverrides([], [new OriginKeyPair("input|one", "input|two")]);

        var result = DuplicateMatcher.Match([first, second, other], overrides, keys);

        result.Groups.Count.ShouldBe(3);
        result.Groups.SelectMany(group => group.MemberKeys).ShouldBe(keys);
        result.Suggestions.ShouldContain(suggestion =>
            string.Equals(suggestion.FirstOriginKey, "input|one", StringComparison.Ordinal)
            && string.Equals(suggestion.SecondOriginKey, "input|three", StringComparison.Ordinal));
    }

    [Fact]
    public void Match_rejects_supplied_origin_keys_that_are_misaligned_or_repeated()
    {
        var first = Login("a", "Mail", "alice", "https://mail.example.com");
        var second = Login("b", "Bank", "bob", "https://bank.example.org");

        var misaligned = () => DuplicateMatcher.Match([first, second], null, ["input|one"]);
        var repeated = () => DuplicateMatcher.Match([first, second], null, ["input|one", "input|one"]);

        misaligned.ShouldThrow<ArgumentException>();
        repeated.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Match_forced_link_and_split_overrides_apply()
    {
        var first = Login("a", "Mail", "alice", "https://mail.example.com");
        var duplicate = Login("b", "Mail", "alice", "https://mail.example.com");
        var other = Login("c", "Bank", "carol", "https://bank.example.org");
        var overrides = new MatchOverrides(
            [new OriginKeyPair("bitwarden|vault.json|a", "bitwarden|vault.json|c")],
            [new OriginKeyPair("bitwarden|vault.json|a", "bitwarden|vault.json|b")]);

        var result = DuplicateMatcher.Match([first, duplicate, other], overrides);

        result.Groups.Count.ShouldBe(2);
        result.Groups[0].Members.ShouldBe([first, other]);
        result.Groups[1].Members.ShouldBe([duplicate]);
    }
}
