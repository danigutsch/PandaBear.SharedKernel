using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Model;
using static PandaBear.VaultManager.Tool.Tests.Matching.MatchFixture;

namespace PandaBear.VaultManager.Tool.Tests.Matching;

public sealed class OriginKeysTests
{
    [Fact]
    public void Compute_numbers_repeated_keys_in_input_order()
    {
        VaultItem[] items = [Login("x", "One", "alice"), Login("x", "Two", "bob"), Login("x", "Three", "carol")];

        var keys = OriginKeys.Compute(items);

        keys.ShouldBe(["bitwarden|vault.json|x", "bitwarden|vault.json|x#2", "bitwarden|vault.json|x#3"]);
    }

    [Fact]
    public void Compute_keeps_keys_unique_when_an_original_ID_already_ends_with_a_number_suffix()
    {
        VaultItem[] items = [Login("x#2", "One", "alice"), Login("x", "Two", "bob"), Login("x", "Three", "carol")];

        var keys = OriginKeys.Compute(items);

        keys.ShouldBe(["bitwarden|vault.json|x#2", "bitwarden|vault.json|x", "bitwarden|vault.json|x#3"]);
    }

    [Fact]
    public void Compute_lets_an_original_ID_with_a_number_suffix_keep_its_key_when_a_repeated_key_comes_first()
    {
        VaultItem[] items = [Login("x", "One", "alice"), Login("x", "Two", "bob"), Login("x#2", "Three", "carol"), Login("x#2", "Four", "dave")];

        var keys = OriginKeys.Compute(items);

        keys.ShouldBe(["bitwarden|vault.json|x", "bitwarden|vault.json|x#3", "bitwarden|vault.json|x#2", "bitwarden|vault.json|x#2#2"]);
    }

    [Fact]
    public void Compute_content_key_does_not_depend_on_the_user_name()
    {
        VaultItem secretHandle = Login(string.Empty, "Mail", "alice.secret.handle@example.com", "https://mail.example.com");
        VaultItem guessedHandle = Login(string.Empty, "Mail", "alice@example.com", "https://mail.example.com");

        string secretKey = OriginKeys.Compute([secretHandle]).ShouldHaveSingleItem();
        string guessedKey = OriginKeys.Compute([guessedHandle]).ShouldHaveSingleItem();

        secretKey.ShouldStartWith("bitwarden|vault.json|content:", StringComparison.Ordinal);
        secretKey.ShouldBe(guessedKey);
    }

    [Theory]
    [InlineData("postgres://app:fixture-db-secret@db.internal/prod", "postgres://app:fixture-guess@db.internal/prod")]
    [InlineData("admin:fixture-router-secret@192.168.1.1", "admin:fixture-guess@192.168.1.1")]
    [InlineData("ftp://files.example.com/in?token=fixture-token", "ftp://files.example.com/in?token=fixture-guess")]
    public void Compute_content_key_does_not_depend_on_credentials_in_the_first_address(string stored, string guessed)
    {
        string storedKey = OriginKeys.Compute([Login(string.Empty, "Database", "app", stored)]).ShouldHaveSingleItem();
        string guessedKey = OriginKeys.Compute([Login(string.Empty, "Database", "app", guessed)]).ShouldHaveSingleItem();

        storedKey.ShouldStartWith("bitwarden|vault.json|content:", StringComparison.Ordinal);
        storedKey.ShouldBe(guessedKey);
    }

    [Fact]
    public void Match_groups_items_whose_original_IDs_end_with_number_suffixes()
    {
        VaultItem[] items = [Login("x#2", "One", "alice", "https://one.example.com"), Login("x", "Two", "bob", "https://two.example.com"), Login("x", "Three", "carol", "https://three.example.com")];

        var result = DuplicateMatcher.Match(items);

        result.Groups.SelectMany(group => group.MemberKeys).ShouldBe(["bitwarden|vault.json|x#2", "bitwarden|vault.json|x", "bitwarden|vault.json|x#3"]);
    }
}
