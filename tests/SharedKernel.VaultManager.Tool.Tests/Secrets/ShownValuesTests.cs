using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Secrets;
using PandaBear.VaultManager.Tool.Tests.Merge;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Tests which field and address values the review shows and which it masks or withholds.</summary>
public sealed class ShownValuesTests
{
    /// <summary>A text field without a finding is shown.</summary>
    [Fact]
    public void A_text_field_without_a_finding_is_shown()
    {
        // Arrange
        var field = new CustomField("Plan", "Team", CustomFieldType.Text);

        // Act
        var shown = ShownValues.IsShown(field, 0, [FindingSamples.At(SecretLocation.FieldValue(1))]);

        // Assert
        shown.ShouldBeTrue();
    }

    /// <summary>A hidden, boolean, or linked field is not shown.</summary>
    [Theory]
    [InlineData((int)CustomFieldType.Hidden)]
    [InlineData((int)CustomFieldType.Boolean)]
    [InlineData((int)CustomFieldType.Linked)]
    public void A_field_that_is_not_text_is_not_shown(int type)
    {
        // Arrange
        var field = new CustomField("Remember", "true", (CustomFieldType)type);

        // Act
        var shown = ShownValues.IsShown(field, 0, []);

        // Assert
        shown.ShouldBeFalse();
    }

    /// <summary>A text field with a finding in its value or name is not shown.</summary>
    [Fact]
    public void A_text_field_with_a_finding_in_its_value_or_name_is_not_shown()
    {
        // Arrange
        var field = new CustomField("Backup", "fixture value", CustomFieldType.Text);

        // Act
        var shownWithValueFinding = ShownValues.IsShown(field, 2, [FindingSamples.At(SecretLocation.FieldValue(2))]);
        var shownWithNameFinding = ShownValues.IsShown(field, 2, [FindingSamples.At(SecretLocation.FieldName(2))]);

        // Assert
        shownWithValueFinding.ShouldBeFalse();
        shownWithNameFinding.ShouldBeFalse();
    }

    /// <summary>The additional folders field is recognized and not shown as a text field.</summary>
    [Fact]
    public void The_additional_folders_field_is_not_shown_as_a_text_field()
    {
        // Arrange
        var field = new CustomField(AdditionalFolders.FieldName, "Personal", CustomFieldType.Text);
        var hiddenField = field with
        {
            Type = CustomFieldType.Hidden
        };

        // Act
        var isAdditionalFolders = ShownValues.IsAdditionalFolders(field);
        var shown = ShownValues.IsShown(field, 0, []);
        var hiddenIsAdditionalFolders = ShownValues.IsAdditionalFolders(hiddenField);

        // Assert
        isAdditionalFolders.ShouldBeTrue();
        shown.ShouldBeFalse();
        hiddenIsAdditionalFolders.ShouldBeFalse();
    }

    /// <summary>Addresses with user info, a query, or a fragment are masked after the part that is safe to show.</summary>
    [Theory]
    [InlineData("https://alice:fixture-uri-secret@example.com/reset?token=fixture-uri-token", "https://***@example.com/reset?***")]
    [InlineData("https://example.com/login#fixture-fragment", "https://example.com/login#***")]
    [InlineData("ftp://user:fixture-ftp-secret@files.example.com/in", "ftp://***@files.example.com/in")]
    [InlineData("admin:fixture-bare-secret@192.168.1.1", "***@192.168.1.1")]
    [InlineData("https://admin:fixture/slash@router.local", "https://***")]
    [InlineData("https://admin:fixture#hash@router.local", "https://***")]
    [InlineData("https://admin:fixture?query@router.local", "https://***")]
    [InlineData("https://evil.example\\@bank.example", "https://***")]
    [InlineData("https://example.com\\reset?token=fixture-token", "https://example.com\\reset?***")]
    [InlineData("example.com/reset?token=fixture-token&next=https://other.example", "example.com/reset?***")]
    public void Addresses_with_user_info_a_query_or_a_fragment_are_masked(string address, string masked)
    {
        // Act
        var shown = ShownValues.MaskUri(address);
        var isMasked = ShownValues.IsMasked(address);

        // Assert
        shown.ShouldBe(masked);
        isMasked.ShouldBeTrue();
    }

    /// <summary>Addresses that hold a secret finding are masked after their scheme.</summary>
    [Theory]
    [InlineData("slack-webhook-url", "https://***")]
    [InlineData("connection-string", "***")]
    public void Addresses_that_hold_a_secret_finding_are_masked_after_their_scheme(string ruleId, string masked)
    {
        // Arrange
        string address = RuleSamples.All.ShouldHaveSingleItem(sample => sample.RuleId == ruleId).Positive;

        // Act
        var shown = ShownValues.MaskUri(address);

        // Assert
        shown.ShouldBe(masked);
    }

    /// <summary>A user name that holds a secret finding is withheld.</summary>
    [Fact]
    public void A_user_name_that_holds_a_secret_finding_is_withheld()
    {
        // Arrange
        var item = new VaultItem
        {
            Type = VaultItemType.Login,
            Name = "Deploy",
            Login = new LoginData { Username = TestSecrets.GitHubToken("ghp", 5) },
            Origin = new ItemOrigin("bitwarden", "vault.json", "item-1", null),
        };

        // Act
        var username = ShownValues.Username(item, []);

        // Assert
        username.ShouldBeNull();
    }

    /// <summary>Addresses without user info, a query, or a fragment are shown as they are.</summary>
    [Fact]
    public void Addresses_without_user_info_a_query_or_a_fragment_are_shown_as_they_are()
    {
        // Act
        var shown = ShownValues.MaskUri("https://github.com/login");
        var isMasked = ShownValues.IsMasked("https://github.com/login");

        // Assert
        shown.ShouldBe("https://github.com/login");
        isMasked.ShouldBeFalse();
    }

    /// <summary>A user name is withheld while a finding lies in it and shown when the finding lies elsewhere.</summary>
    [Fact]
    public void A_user_name_is_withheld_while_a_finding_lies_in_it()
    {
        // Arrange
        var item = new VaultItem
        {
            Type = VaultItemType.Login,
            Name = "GitHub",
            Login = new LoginData { Username = "alice" },
            Origin = new ItemOrigin("bitwarden", "vault.json", "item-1", null),
        };
        var note = item with
        {
            Login = null,
            Type = VaultItemType.SecureNote
        };

        // Act
        var withNotesFinding = ShownValues.Username(item, [FindingSamples.At(SecretLocation.Notes)]);
        var withUsernameFinding = ShownValues.Username(item, [FindingSamples.At(SecretLocation.LoginUsername)]);
        var noteUsername = ShownValues.Username(note, []);

        // Assert
        withNotesFinding.ShouldBe("alice");
        withUsernameFinding.ShouldBeNull();
        noteUsername.ShouldBeNull();
    }
}
