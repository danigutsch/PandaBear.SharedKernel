using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Bitwarden;

/// <summary>Tests the reader for Bitwarden CSV exports.</summary>
public sealed class BitwardenCsvReaderTests
{
    /// <summary>The individual.csv login row maps its folder path, favorite and reprompt flags, notes, origin, custom fields, TOTP, username, password, and both URIs.</summary>
    [Fact]
    public void Read_individual_export_maps_login_fields_uris_and_flags()
    {
        // Act
        var result = BitwardenCsvReader.Read(Fixture.Read("bitwarden-csv/individual.csv"), "individual.csv");

        // Assert
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

    /// <summary>The "Plain note" row becomes a secure note without login or folder, keeps its multi-line notes, and gets a conversion note about the archived state.</summary>
    [Fact]
    public void Read_individual_export_maps_notes_and_records_archived_items()
    {
        // Act
        var items = BitwardenCsvReader.Read(Fixture.Read("bitwarden-csv/individual.csv"), "individual.csv").Export.Items;

        // Assert
        var item = items.ShouldHaveSingleItem(item => item.Name == "Plain note");
        item.Type.ShouldBe(VaultItemType.SecureNote);
        item.Login.ShouldBeNull();
        item.FolderPath.ShouldBeNull();
        item.Favorite.ShouldBeFalse();
        item.Notes.ShouldBe("multi\nline");
        item.ConversionNotes.ShouldContain(note => note.Contains("archived", StringComparison.Ordinal));
    }

    /// <summary>In organization.csv the first collection "Shared" becomes the folder path, the second goes into an "Additional folders" field, and a conversion note mentions collections.</summary>
    [Fact]
    public void Read_organization_export_uses_first_collection_as_folder()
    {
        // Act
        var items = BitwardenCsvReader.Read(Fixture.Read("bitwarden-csv/organization.csv"), "organization.csv").Export.Items;

        // Assert
        var item = items.ShouldHaveSingleItem();
        item.FolderPath.ShouldBe("Shared");
        item.Fields.ShouldBe([new CustomField("Additional folders", "Other", CustomFieldType.Text)]);
        item.ConversionNotes.ShouldContain(note => note.Contains("collections", StringComparison.Ordinal));
        item.Login.ShouldNotBeNull().Username.ShouldBe("bob");
    }

    /// <summary>Reading a CSV export adds a warning that mentions JSON as the more complete format.</summary>
    [Fact]
    public void Read_warns_that_csv_omits_data_and_recommends_JSON()
    {
        // Act
        var result = BitwardenCsvReader.Read(Fixture.Read("bitwarden-csv/organization.csv"), "organization.csv");

        // Assert
        result.Warnings.ShouldContain(warning => warning.Contains("JSON", StringComparison.Ordinal));
    }

    /// <summary>A data row with one column too few throws InvalidDataException naming line 2 without echoing the row's secret value.</summary>
    [Fact]
    public void Read_row_with_wrong_column_count_throws_with_line_number_and_no_value()
    {
        // Arrange
        const string csv = "folder,favorite,type,name,notes,fields,reprompt,login_uri,login_username,login_password,login_totp\n,,login,n,,,0,,user,secret-value";
        var action = () => BitwardenCsvReader.Read(csv, "bad.csv");

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain("line 2", StringComparison.Ordinal);
        exception.Message.ShouldNotContain("secret-value", StringComparison.Ordinal);
    }

    /// <summary>A CSV whose header is "a,b,c" instead of the Bitwarden columns throws InvalidDataException.</summary>
    [Fact]
    public void Read_without_bitwarden_header_throws_invalid_data()
    {
        // Arrange
        var action = () => BitwardenCsvReader.Read("a,b,c\n1,2,3", "other.csv");

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }
}
