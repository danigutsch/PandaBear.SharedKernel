using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Secrets;
using PandaBear.VaultManager.Tool.Tests.Merge;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

public sealed class ShownValuesTests
{
    [Fact]
    public void A_text_field_without_a_finding_is_shown()
    {
        var field = new CustomField("Plan", "Team", CustomFieldType.Text);

        ShownValues.IsShown(field, 0, [FindingSamples.At(SecretLocation.FieldValue(1))]).ShouldBeTrue();
    }

    [Theory]
    [InlineData((int)CustomFieldType.Hidden)]
    [InlineData((int)CustomFieldType.Boolean)]
    [InlineData((int)CustomFieldType.Linked)]
    public void A_field_that_is_not_text_is_not_shown(int type)
    {
        var field = new CustomField("Remember", "true", (CustomFieldType)type);

        ShownValues.IsShown(field, 0, []).ShouldBeFalse();
    }

    [Fact]
    public void A_text_field_with_a_finding_in_its_value_or_name_is_not_shown()
    {
        var field = new CustomField("Backup", "fixture value", CustomFieldType.Text);

        ShownValues.IsShown(field, 2, [FindingSamples.At(SecretLocation.FieldValue(2))]).ShouldBeFalse();
        ShownValues.IsShown(field, 2, [FindingSamples.At(SecretLocation.FieldName(2))]).ShouldBeFalse();
    }

    [Fact]
    public void The_additional_folders_field_is_not_shown_as_a_text_field()
    {
        var field = new CustomField(AdditionalFolders.FieldName, "Personal", CustomFieldType.Text);

        ShownValues.IsAdditionalFolders(field).ShouldBeTrue();
        ShownValues.IsShown(field, 0, []).ShouldBeFalse();
        ShownValues.IsAdditionalFolders(field with { Type = CustomFieldType.Hidden }).ShouldBeFalse();
    }

    [Theory]
    [InlineData("https://alice:fixture-uri-secret@example.com/reset?token=fixture-uri-token", "https://***@example.com/reset?***")]
    [InlineData("https://example.com/login#fixture-fragment", "https://example.com/login#***")]
    [InlineData("ftp://user:fixture-ftp-secret@files.example.com/in", "ftp://***@files.example.com/in")]
    public void Addresses_with_user_info_a_query_or_a_fragment_are_masked(string address, string masked)
    {
        ShownValues.MaskUri(address).ShouldBe(masked);
        ShownValues.IsMasked(address).ShouldBeTrue();
    }

    [Fact]
    public void Addresses_without_user_info_a_query_or_a_fragment_are_shown_as_they_are()
    {
        ShownValues.MaskUri("https://github.com/login").ShouldBe("https://github.com/login");
        ShownValues.IsMasked("https://github.com/login").ShouldBeFalse();
    }

    [Fact]
    public void A_user_name_is_withheld_while_a_finding_lies_in_it()
    {
        var item = new VaultItem
        {
            Type = VaultItemType.Login,
            Name = "GitHub",
            Login = new LoginData { Username = "alice" },
            Origin = new ItemOrigin("bitwarden", "vault.json", "item-1", null),
        };

        ShownValues.Username(item, [FindingSamples.At(SecretLocation.Notes)]).ShouldBe("alice");
        ShownValues.Username(item, [FindingSamples.At(SecretLocation.LoginUsername)]).ShouldBeNull();
        ShownValues.Username(item with { Login = null, Type = VaultItemType.SecureNote }, []).ShouldBeNull();
    }
}
