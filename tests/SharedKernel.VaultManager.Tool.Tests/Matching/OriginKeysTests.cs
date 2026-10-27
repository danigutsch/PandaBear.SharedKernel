using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Model;
using static PandaBear.VaultManager.Tool.Tests.Matching.MatchFixture;

namespace PandaBear.VaultManager.Tool.Tests.Matching;

/// <summary>Tests <see cref="OriginKeys"/> key computation and its effect on duplicate matching.</summary>
public sealed class OriginKeysTests
{
    [Fact]
    public void Compute_numbers_repeated_keys_in_input_order()
    {
        // Arrange
        VaultItem[] items = [Login("x", "One", "alice"), Login("x", "Two", "bob"), Login("x", "Three", "carol")];

        // Act
        var keys = OriginKeys.Compute(items);

        // Assert
        keys.ShouldBe(["bitwarden|vault.json|x", "bitwarden|vault.json|x#2", "bitwarden|vault.json|x#3"]);
    }

    [Fact]
    public void Compute_keeps_keys_unique_when_an_original_ID_already_ends_with_a_number_suffix()
    {
        // Arrange
        VaultItem[] items = [Login("x#2", "One", "alice"), Login("x", "Two", "bob"), Login("x", "Three", "carol")];

        // Act
        var keys = OriginKeys.Compute(items);

        // Assert
        keys.ShouldBe(["bitwarden|vault.json|x#2", "bitwarden|vault.json|x", "bitwarden|vault.json|x#3"]);
    }

    [Fact]
    public void Compute_lets_an_original_ID_with_a_number_suffix_keep_its_key_when_a_repeated_key_comes_first()
    {
        // Arrange
        VaultItem[] items = [Login("x", "One", "alice"), Login("x", "Two", "bob"), Login("x#2", "Three", "carol"), Login("x#2", "Four", "dave")];

        // Act
        var keys = OriginKeys.Compute(items);

        // Assert
        keys.ShouldBe(["bitwarden|vault.json|x", "bitwarden|vault.json|x#3", "bitwarden|vault.json|x#2", "bitwarden|vault.json|x#2#2"]);
    }

    [Fact]
    public void Compute_content_key_does_not_depend_on_the_user_name()
    {
        // Arrange
        VaultItem secretHandle = Login(string.Empty, "Mail", "alice.secret.handle@example.com", "https://mail.example.com");
        VaultItem guessedHandle = Login(string.Empty, "Mail", "alice@example.com", "https://mail.example.com");

        // Act
        var secretKeys = OriginKeys.Compute([secretHandle]);
        var guessedKeys = OriginKeys.Compute([guessedHandle]);

        // Assert
        string secretKey = secretKeys.ShouldHaveSingleItem();
        string guessedKey = guessedKeys.ShouldHaveSingleItem();
        secretKey.ShouldStartWith("bitwarden|vault.json|content:", StringComparison.Ordinal);
        secretKey.ShouldBe(guessedKey);
    }

    [Theory]
    [InlineData("postgres://app:fixture-db-secret@db.internal/prod", "postgres://app:fixture-guess@db.internal/prod")]
    [InlineData("admin:fixture-router-secret@192.168.1.1", "admin:fixture-guess@192.168.1.1")]
    [InlineData("ftp://files.example.com/in?token=fixture-token", "ftp://files.example.com/in?token=fixture-guess")]
    public void Compute_content_key_does_not_depend_on_credentials_in_the_first_address(string stored, string guessed)
    {
        // Act
        var storedKeys = OriginKeys.Compute([Login(string.Empty, "Database", "app", stored)]);
        var guessedKeys = OriginKeys.Compute([Login(string.Empty, "Database", "app", guessed)]);

        // Assert
        string storedKey = storedKeys.ShouldHaveSingleItem();
        string guessedKey = guessedKeys.ShouldHaveSingleItem();
        storedKey.ShouldStartWith("bitwarden|vault.json|content:", StringComparison.Ordinal);
        storedKey.ShouldBe(guessedKey);
    }

    [Fact]
    public void Match_groups_items_whose_original_IDs_end_with_number_suffixes()
    {
        // Arrange
        VaultItem[] items = [Login("x#2", "One", "alice", "https://one.example.com"), Login("x", "Two", "bob", "https://two.example.com"), Login("x", "Three", "carol", "https://three.example.com")];

        // Act
        var result = DuplicateMatcher.Match(items);

        // Assert
        result.Groups.SelectMany(group => group.MemberKeys).ShouldBe(["bitwarden|vault.json|x#2", "bitwarden|vault.json|x", "bitwarden|vault.json|x#3"]);
    }
}
