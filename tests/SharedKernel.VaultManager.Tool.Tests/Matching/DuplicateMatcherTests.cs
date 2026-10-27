using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Model;
using static PandaBear.VaultManager.Tool.Tests.Matching.MatchFixture;

namespace PandaBear.VaultManager.Tool.Tests.Matching;

/// <summary>Tests how the duplicate matcher groups vault items and suggests related items.</summary>
public sealed class DuplicateMatcherTests
{
    /// <summary>Two logins with the same host and a username that differs only in case and trailing space form one group with both members and origin keys and the status Different.</summary>
    [Fact]
    public void Match_same_host_and_username_groups_items()
    {
        // Arrange
        var first = Login("a", "Mail", "alice", "https://mail.example.com");
        var second = Login("b", "Webmail", "Alice ", "https://mail.example.com/inbox");

        // Act
        var result = DuplicateMatcher.Match([first, second]);

        // Assert
        var group = result.Groups.ShouldHaveSingleItem();
        group.Members.ShouldBe([first, second]);
        group.MemberKeys.ShouldBe(["bitwarden|vault.json|a", "bitwarden|vault.json|b"]);
        group.Status.ShouldBe(MatchStatus.Different);
    }

    /// <summary>Logins whose addresses differ by a www prefix, scheme, port, path, query, or host case still form one group of three.</summary>
    [Fact]
    public void Match_www_scheme_and_path_are_ignored()
    {
        // Arrange
        VaultItem[] items =
        [
            Login("a", "One", "alice", "https://www.Example.com:8443/login?next=1#top"),
            Login("b", "Two", "alice", "example.com/account"),
            Login("c", "Three", "alice", "http://EXAMPLE.COM"),
        ];

        // Act
        var result = DuplicateMatcher.Match(items);

        // Assert
        result.Groups.ShouldHaveSingleItem().Members.Count.ShouldBe(3);
    }

    /// <summary>Two logins on the same host with the usernames alice and bob form two separate groups, both with the status Unique.</summary>
    [Fact]
    public void Match_same_host_different_username_stays_separate()
    {
        // Arrange
        var alice = Login("a", "Mail", "alice", "https://mail.example.com");
        var bob = Login("b", "Mail", "bob", "https://mail.example.com");

        // Act
        var result = DuplicateMatcher.Match([alice, bob]);

        // Assert
        result.Groups.Count.ShouldBe(2);
        result.Groups.ShouldAllSatisfy(group => group.Status.ShouldBe(MatchStatus.Unique));
    }

    /// <summary>Two notes whose titles differ only in spacing and case group together, a login with the same title stays in its own group, and the matcher suggests the same-title relation.</summary>
    [Fact]
    public void Match_title_and_type_fallback_without_uri()
    {
        // Arrange
        var first = Note("n1", "Home  WiFi");
        var second = Note("n2", " home wifi");
        var login = Login("l1", "Home WiFi", null);

        // Act
        var result = DuplicateMatcher.Match([first, second, login]);

        // Assert
        result.Groups.Count.ShouldBe(2);
        result.Groups[0].Members.ShouldBe([first, second]);
        result.Groups[1].Members.ShouldBe([login]);
        result.Suggestions.ShouldContain(suggestion => suggestion.Kind == MatchSuggestionKind.SameTitle);
    }

    /// <summary>Logins for alice and bob on hosts that differ only by a www prefix stay in two groups and yield one same-host-different-username suggestion with both origin keys and names.</summary>
    [Fact]
    public void Match_same_host_only_is_suggested_not_grouped()
    {
        // Arrange
        var alice = Login("a", "Mail", "alice", "https://mail.example.com");
        var bob = Login("b", "Mail for Bob", "bob", "https://www.mail.example.com");

        // Act
        var result = DuplicateMatcher.Match([alice, bob]);

        // Assert
        result.Groups.Count.ShouldBe(2);
        var suggestion = result.Suggestions.ShouldHaveSingleItem();
        suggestion.Kind.ShouldBe(MatchSuggestionKind.SameHostDifferentUsername);
        suggestion.FirstOriginKey.ShouldBe("bitwarden|vault.json|a");
        suggestion.SecondOriginKey.ShouldBe("bitwarden|vault.json|b");
        suggestion.FirstName.ShouldBe("Mail");
        suggestion.SecondName.ShouldBe("Mail for Bob");
    }

    /// <summary>Group keys are 16 lowercase hex digits and are the same for equal items without IDs, copies of the items, and a reordered input, with three different content-based origin keys.</summary>
    [Fact]
    public void Match_group_keys_are_stable_across_runs()
    {
        // Arrange
        VaultItem[] items =
        [
            Login("a", "Mail", "alice", "https://mail.example.com"),
            Login("b", "Mail", "alice", "https://mail.example.com"),
            Note(null, "Without id"),
            Note(null, "Without id"),
            Note(null, "Other"),
        ];

        // Act
        var first = DuplicateMatcher.Match(items).Groups.Select(group => group.Key).ToArray();
        var second = DuplicateMatcher.Match([.. items.Select(item => item with { })]).Groups.Select(group => group.Key).ToArray();
        var reversed = DuplicateMatcher.Match([items[1], items[0], .. items[2..]]).Groups.Select(group => group.Key).ToArray();
        string[] contentKeys = [.. OriginKeys.Compute(items).Skip(2)];

        // Assert
        second.ShouldBe(first);
        reversed.ShouldBe(first);
        first.Length.ShouldBe(3);
        first.ShouldAllSatisfy(key => key.ShouldMatch("^[0-9a-f]{16}$"));
        contentKeys.ShouldAllSatisfy(key => key.ShouldStartWith("keeper|keeper.json|content:", StringComparison.Ordinal));
        contentKeys.Distinct(StringComparer.Ordinal).Count().ShouldBe(3);
    }

    /// <summary>With caller-supplied origin keys and a split override between the first two duplicates, the matcher yields three groups that report those keys and suggests the first and third items.</summary>
    [Fact]
    public void Match_with_supplied_origin_keys_uses_them_for_groups_overrides_and_suggestions()
    {
        // Arrange
        var first = Login("a", "Mail", "alice", "https://mail.example.com");
        var second = Login("b", "Mail", "alice", "https://mail.example.com");
        var other = Login("c", "Mail", "bob", "https://mail.example.com");
        string[] keys = ["input|one", "input|two", "input|three"];
        var overrides = new MatchOverrides([], [new OriginKeyPair("input|one", "input|two")]);

        // Act
        var result = DuplicateMatcher.Match([first, second, other], overrides, keys);

        // Assert
        result.Groups.Count.ShouldBe(3);
        result.Groups.SelectMany(group => group.MemberKeys).ShouldBe(keys);
        result.Suggestions.ShouldContain(suggestion =>
            string.Equals(suggestion.FirstOriginKey, "input|one", StringComparison.Ordinal)
            && string.Equals(suggestion.SecondOriginKey, "input|three", StringComparison.Ordinal));
    }

    /// <summary>Supplying one origin key for two items, or the same key twice, throws ArgumentException.</summary>
    [Fact]
    public void Match_rejects_supplied_origin_keys_that_are_misaligned_or_repeated()
    {
        // Arrange
        var first = Login("a", "Mail", "alice", "https://mail.example.com");
        var second = Login("b", "Bank", "bob", "https://bank.example.org");
        var misaligned = () => DuplicateMatcher.Match([first, second], null, ["input|one"]);
        var repeated = () => DuplicateMatcher.Match([first, second], null, ["input|one", "input|one"]);

        // Act, Assert
        misaligned.ShouldThrow<ArgumentException>();
        repeated.ShouldThrow<ArgumentException>();
    }

    /// <summary>A forced link joins two unlike logins into one group and a split separates two duplicates, leaving the groups [first, other] and [duplicate].</summary>
    [Fact]
    public void Match_forced_link_and_split_overrides_apply()
    {
        // Arrange
        var first = Login("a", "Mail", "alice", "https://mail.example.com");
        var duplicate = Login("b", "Mail", "alice", "https://mail.example.com");
        var other = Login("c", "Bank", "carol", "https://bank.example.org");
        var overrides = new MatchOverrides(
            [new OriginKeyPair("bitwarden|vault.json|a", "bitwarden|vault.json|c")],
            [new OriginKeyPair("bitwarden|vault.json|a", "bitwarden|vault.json|b")]);

        // Act
        var result = DuplicateMatcher.Match([first, duplicate, other], overrides);

        // Assert
        result.Groups.Count.ShouldBe(2);
        result.Groups[0].Members.ShouldBe([first, other]);
        result.Groups[1].Members.ShouldBe([duplicate]);
    }
}
