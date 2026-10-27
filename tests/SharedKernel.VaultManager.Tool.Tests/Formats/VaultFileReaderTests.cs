using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Formats;

/// <summary>Tests that the vault file reader picks the reader for a file's format and reports unreadable input.</summary>
public sealed class VaultFileReaderTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("vault-manager-read-").FullName;

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    /// <summary>Reading a Keeper or Bitwarden JSON or CSV sample returns items whose origin provider is that vendor, without asking for a password.</summary>
    /// <param name="sample">The name of the sample written by the sample factory.</param>
    /// <param name="provider">The expected provider name of every item.</param>
    [Theory]
    [InlineData("keeper-json", "keeper")]
    [InlineData("keeper-csv", "keeper")]
    [InlineData("bitwarden-json", "bitwarden")]
    [InlineData("bitwarden-csv", "bitwarden")]
    public void Read_dispatches_to_the_reader_for_the_detected_format(string sample, string provider)
    {
        // Arrange
        string path = FormatSamples.WriteSample(directory, sample);

        // Act
        var result = VaultFileReader.Read(path, _ => throw new InvalidOperationException("No password expected."));

        // Assert
        result.Export.Items.ShouldNotBeEmpty();
        result.Export.Items.ShouldAllSatisfy(item => item.Origin.Provider.ShouldBe(provider));
    }

    /// <summary>An export with one or two items that carry a deletion date yields only the kept item and a singular or plural warning that tells the user to restore and export again.</summary>
    /// <param name="deleted">The number of trashed items in the export.</param>
    /// <param name="warning">The expected warning text.</param>
    [Theory]
    [InlineData(1, "1 item in the trash was skipped. Restore it in Bitwarden and export again to keep it.")]
    [InlineData(2, "2 items in the trash were skipped. Restore them in Bitwarden and export again to keep them.")]
    public void Read_warns_about_the_items_in_the_trash_that_it_skips(int deleted, string warning)
    {
        // Arrange
        string trash = string.Concat(Enumerable.Range(0, deleted).Select(index =>
            ",{\"id\":\"old-" + index + "\",\"type\":2,\"name\":\"Old\",\"notes\":\"fixture keep me\",\"deletedDate\":\"2026-01-01T00:00:00Z\"}"));
        string path = Path.Combine(directory, "trash.json");
        File.WriteAllText(path, "{\"encrypted\":false,\"folders\":[],\"items\":[{\"id\":\"kept\",\"type\":2,\"name\":\"Kept\",\"notes\":\"fixture\"}" + trash + "]}");

        // Act
        var result = VaultFileReader.Read(path, _ => throw new InvalidOperationException("No password expected."));

        // Assert
        result.Export.Items.ShouldHaveSingleItem().Name.ShouldBe("Kept");
        result.Warnings.ShouldBe([warning]);
    }

    /// <summary>A password-protected export makes the reader ask for the password once, passing the file name protected.json, and then returns items.</summary>
    [Fact]
    public void Read_password_protected_export_asks_for_the_file_password_by_file_name()
    {
        // Arrange
        string path = FormatSamples.WriteSample(directory, "bitwarden-protected");
        var asked = new List<string>();

        // Act
        var result = VaultFileReader.Read(path, name =>
        {
            asked.Add(name);
            return "fixture password";
        });

        // Assert
        asked.ShouldBe(["protected.json"]);
        result.Export.Items.ShouldNotBeEmpty();
    }

    /// <summary>An account-restricted Bitwarden export throws InvalidDataException whose message says it is account-restricted, without asking for a password.</summary>
    [Fact]
    public void Read_account_restricted_export_throws_with_explanation()
    {
        // Arrange
        string path = FormatSamples.WriteSample(directory, "bitwarden-restricted");
        var action = () => VaultFileReader.Read(path, _ => "unused");

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain("account-restricted", StringComparison.Ordinal);
    }

    /// <summary>A plain text file in no known format throws InvalidDataException.</summary>
    [Fact]
    public void Read_unknown_format_throws_invalid_data()
    {
        // Arrange
        string path = FormatSamples.WriteSample(directory, "other-text");
        var action = () => VaultFileReader.Read(path, _ => "unused");

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }

    /// <summary>An input path that is a directory named export.json throws IOException whose message holds the path and says it is a directory.</summary>
    [Fact]
    public void Read_reports_an_input_that_is_a_directory_by_its_path()
    {
        // Arrange
        string path = Directory.CreateDirectory(Path.Combine(directory, "export.json")).FullName;
        var action = () => VaultFileReader.Read(path, _ => "unused");

        // Act
        var exception = action.ShouldThrow<IOException>();

        // Assert
        exception.Message.ShouldContain(path, StringComparison.Ordinal);
        exception.Message.ShouldContain("is a directory", StringComparison.Ordinal);
    }

    /// <summary>A sparse file one byte over the maximum input size throws InvalidDataException saying it is too large.</summary>
    [Fact]
    public void Read_rejects_files_larger_than_the_input_cap_before_reading()
    {
        // Arrange
        string path = Path.Combine(directory, "large.json");
        using (var stream = File.Create(path))
        {
            stream.SetLength(VaultFileReader.MaxInputBytes + 1);
        }

        var action = () => VaultFileReader.Read(path, _ => "unused");

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain("too large", StringComparison.Ordinal);
    }

    /// <summary>A Keeper zip holding export.json with one login record returns that record as a login item.</summary>
    [Fact]
    public void Read_zip_exports_return_items_from_the_archive()
    {
        // Arrange
        string keeper = FormatSamples.Write(directory, "keeper.zip", TestZip.Create(
            ("export.json", TestZip.Utf8("""{"records":[{"uid":"u","title":"Zipped","login":"x"}]}"""))));

        // Act
        var result = VaultFileReader.Read(keeper, _ => "unused");

        // Assert
        result.Export.Items.ShouldHaveSingleItem().Type.ShouldBe(VaultItemType.Login);
    }
}
