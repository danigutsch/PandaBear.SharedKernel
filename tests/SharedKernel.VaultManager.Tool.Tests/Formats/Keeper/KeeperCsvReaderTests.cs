using PandaBear.VaultManager.Tool.Formats.Keeper;
using PandaBear.VaultManager.Tool.Model;
using static PandaBear.VaultManager.Tool.Tests.Formats.Keeper.KeeperTestData;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Keeper;

public sealed class KeeperCsvReaderTests
{
    [Fact]
    public void Read_commander_login_row_maps_typed_pairs_folders_and_quoted_notes()
    {
        // Act
        var result = KeeperCsvReader.Read(Fixture.Read("keeper/commander.csv"), "commander.csv");

        // Assert
        var item = result.Export.Items.ShouldHaveSingleItem(item => item.Name == "Example login");
        item.Type.ShouldBe(VaultItemType.Login);
        item.Origin.ShouldBe(new ItemOrigin("keeper", "commander.csv", "rec-login", null));
        item.Notes.ShouldBe("login notes, with \"quotes\"");
        item.FolderPath.ShouldBe("Team/Shared/Web/Sites");
        item.ConversionNotes.ShouldContain(note => note.Contains("permissions", StringComparison.Ordinal));
        var login = item.Login.ShouldNotBeNull();
        login.Totp.ShouldBe("otpauth://totp/Example:alice?secret=JBSWY3DPEHPK3PXP&issuer=Example");
        login.Uris.Select(uri => uri.Value).ShouldBe(["https://example.com/login", "https://accounts.example.com"]);
        item.Fields.ShouldBe(
        [
            Field("TOTP", "otpauth://totp/Example:backup?secret=KRSXG5CTMVRXEZLU&issuer=Example", CustomFieldType.Hidden),
            Field("Nickname", "ali"),
            Field("Active", "true", CustomFieldType.Boolean),
            Field("First pet?", "fixture-answer-1", CustomFieldType.Hidden),
            Field("Plain label", "plain value"),
        ]);
    }

    [Fact]
    public void Read_commander_card_and_passport_rows_fill_typed_slots()
    {
        // Act
        var items = KeeperCsvReader.Read(Fixture.Read("keeper/commander.csv"), "commander.csv").Export.Items;

        // Assert
        var card = items.ShouldHaveSingleItem(item => item.Name == "Example card");
        card.Card.ShouldBe(new CardData
        {
            CardholderName = "Alice Example",
            Number = "4111111111111111",
            ExpMonth = "03",
            ExpYear = "2030",
            Code = "123",
        });
        card.Fields.ShouldBe([Field("PIN", "4321", CustomFieldType.Hidden)]);
        var passport = items.ShouldHaveSingleItem(item => item.Name == "Passport");
        passport.FolderPath.ShouldBe("Identity");
        passport.Identity.ShouldBe(new IdentityData { FirstName = "Alice", MiddleName = "Q", LastName = "Example", PassportNumber = "X1234567" });
        passport.Fields.ShouldBe([Field("birthDate", "1990-01-15")]);
    }

    [Fact]
    public void Read_web_rows_map_english_labels_and_combined_values()
    {
        // Act
        var items = KeeperCsvReader.Read(Fixture.Read("keeper/web.csv"), "web.csv").Export.Items;

        // Assert
        var login = items.ShouldHaveSingleItem(item => item.Name == "Web login");
        login.Origin.ShouldBe(new ItemOrigin("keeper", "web.csv", null, null));
        login.FolderPath.ShouldBe("Personal");
        login.Notes.ShouldBe("multi\nline notes");
        login.Login.ShouldNotBeNull().Totp.ShouldBe("otpauth://totp/Web:web-user?secret=JBSWY3DPEHPK3PXP&issuer=Web");
        login.Fields.ShouldBe([Field("Security Code", "9876", CustomFieldType.Hidden)]);
        var passport = items.ShouldHaveSingleItem(item => item.Name == "Web passport");
        passport.Type.ShouldBe(VaultItemType.Identity);
        passport.Identity.ShouldBe(new IdentityData { FirstName = "Alice", LastName = "Example", PassportNumber = "X7654321" });
        passport.Fields.ShouldBe([Field("Date of Birth", "1990-01-15")]);
        var bank = items.ShouldHaveSingleItem(item => item.Name == "Web bank");
        bank.Type.ShouldBe(VaultItemType.SecureNote);
        bank.Fields.ShouldBe([Field("Bank Account", "Type: Checking, Account Number: 12345678, Routing Number: 021000021", CustomFieldType.Hidden)]);
    }

    [Fact]
    public void Read_warns_that_csv_loses_structure_and_recommends_JSON()
    {
        // Act
        var result = KeeperCsvReader.Read(Fixture.Read("keeper/web.csv"), "web.csv");

        // Assert
        result.Warnings.ShouldContain(warning => warning.Contains("JSON", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_short_rows_are_skipped_with_line_number_warning_and_no_value()
    {
        // Act
        var result = KeeperCsvReader.Read("folder,secret-value\n", "short.csv");

        // Assert
        result.Export.Items.ShouldBeEmpty();
        result.Warnings.ShouldContain(warning => warning.Contains("line 1", StringComparison.Ordinal));
        result.Warnings.ShouldAllSatisfy(warning => warning.ShouldNotContain("secret-value", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_unterminated_quote_throws_invalid_data()
    {
        // Arrange
        var action = () => KeeperCsvReader.Read("a,b,c,d,e,\"open", "bad.csv");

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }
}
