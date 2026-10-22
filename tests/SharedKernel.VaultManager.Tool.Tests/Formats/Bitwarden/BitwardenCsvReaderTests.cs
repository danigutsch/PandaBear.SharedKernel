using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Bitwarden;

public sealed class BitwardenCsvReaderTests
{
    [Fact]
    public void Read_individual_export_maps_login_fields_uris_and_flags()
    {
        var result = BitwardenCsvReader.Read(Fixture.Read("bitwarden-csv/individual.csv"), "individual.csv");

        var item = result.Export.Items.ShouldHaveSingleItem(item => item.Name == "Example, login");
        item.Type.ShouldBe(VaultItemType.Login);
        item.FolderPath.ShouldBe("Work/Servers");
        item.Favorite.ShouldBeTrue();
        item.Reprompt.ShouldBeTrue();
        item.Notes.ShouldBe("note with \"quotes\"");
        item.Origin.ShouldBe(new ItemOrigin("bitwarden", "individual.csv", null, null));
        item.Fields.ShouldBe(
        [
            new CustomField("PIN", "1234", CustomFieldType.Text),
            new CustomField("Label: with", "colons", CustomFieldType.Text),
        ]);
        var login = item.Login.ShouldNotBeNull();
        login.Username.ShouldBe("alice");
        login.Password.ShouldBe("fixture-password-1");
        login.Totp.ShouldBe("otpauth://totp/Example:alice?secret=JBSWY3DPEHPK3PXP");
        login.Uris.Select(uri => uri.Value).ShouldBe(["https://example.com", "https://accounts.example.com"]);
    }

    [Fact]
    public void Read_individual_export_maps_notes_and_records_archived_items()
    {
        var item = BitwardenCsvReader.Read(Fixture.Read("bitwarden-csv/individual.csv"), "individual.csv").Export.Items
            .ShouldHaveSingleItem(item => item.Name == "Plain note");

        item.Type.ShouldBe(VaultItemType.SecureNote);
        item.Login.ShouldBeNull();
        item.FolderPath.ShouldBeNull();
        item.Favorite.ShouldBeFalse();
        item.Notes.ShouldBe("multi\nline");
        item.ConversionNotes.ShouldContain(note => note.Contains("archived", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_organization_export_uses_first_collection_as_folder()
    {
        var item = BitwardenCsvReader.Read(Fixture.Read("bitwarden-csv/organization.csv"), "organization.csv").Export.Items.ShouldHaveSingleItem();

        item.FolderPath.ShouldBe("Shared");
        item.ConversionNotes.ShouldContain(note => note.Contains("collections", StringComparison.Ordinal));
        item.Login.ShouldNotBeNull().Username.ShouldBe("bob");
    }

    [Fact]
    public void Read_warns_that_csv_omits_data_and_recommends_JSON()
    {
        var result = BitwardenCsvReader.Read(Fixture.Read("bitwarden-csv/organization.csv"), "organization.csv");

        result.Warnings.ShouldContain(warning => warning.Contains("JSON", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_row_with_wrong_column_count_throws_with_line_number_and_no_value()
    {
        const string csv = "folder,favorite,type,name,notes,fields,reprompt,login_uri,login_username,login_password,login_totp\n,,login,n,,,0,,user,secret-value";

        var action = () => BitwardenCsvReader.Read(csv, "bad.csv");

        var exception = action.ShouldThrow<InvalidDataException>();

        exception.Message.ShouldContain("line 2", StringComparison.Ordinal);
        exception.Message.ShouldNotContain("secret-value", StringComparison.Ordinal);
    }

    [Fact]
    public void Read_without_bitwarden_header_throws_invalid_data()
    {
        var action = () => BitwardenCsvReader.Read("a,b,c\n1,2,3", "other.csv");

        action.ShouldThrow<InvalidDataException>();
    }
}
