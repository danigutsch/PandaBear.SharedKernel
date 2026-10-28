using PandaBear.Testing;
using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Formats;

/// <summary>Tests that the vault file reader picks the reader for a file's format and reports unreadable input.</summary>
public sealed class VaultFileReaderTests : IDisposable
{
    private readonly TemporaryDirectory temporaryDirectory = new();

    private string DirectoryPath => temporaryDirectory.Path;

    /// <inheritdoc />
    public void Dispose() => temporaryDirectory.Dispose();

    [Theory]
    [InlineData("keeper-json", "keeper")]
    [InlineData("keeper-csv", "keeper")]
    [InlineData("bitwarden-json", "bitwarden")]
    [InlineData("bitwarden-csv", "bitwarden")]
    public void Read_dispatches_to_the_reader_for_the_detected_format(string sample, string provider)
    {
        // Arrange
        string path = FormatSamples.WriteSample(DirectoryPath, sample);

        // Act
        var result = VaultFileReader.Read(path, _ => throw new InvalidOperationException("No password expected."));

        // Assert
        result.Export.Items.ShouldNotBeEmpty();
        result.Export.Items.ShouldAllSatisfy(item => item.Origin.Provider.ShouldBe(provider));
    }

    [Theory]
    [InlineData(1, "1 item in the trash was skipped. Restore it in Bitwarden and export again to keep it.")]
    [InlineData(2, "2 items in the trash were skipped. Restore them in Bitwarden and export again to keep them.")]
    public void Read_warns_about_the_items_in_the_trash_that_it_skips(int deleted, string warning)
    {
        // Arrange
        string trash = string.Concat(Enumerable.Range(0, deleted).Select(index =>
            ",{\"id\":\"old-" + index + "\",\"type\":2,\"name\":\"Old\",\"notes\":\"fixture keep me\",\"deletedDate\":\"2026-01-01T00:00:00Z\"}"));
        string path = Path.Combine(DirectoryPath, "trash.json");
        File.WriteAllText(path, "{\"encrypted\":false,\"folders\":[],\"items\":[{\"id\":\"kept\",\"type\":2,\"name\":\"Kept\",\"notes\":\"fixture\"}" + trash + "]}");

        // Act
        var result = VaultFileReader.Read(path, _ => throw new InvalidOperationException("No password expected."));

        // Assert
        result.Export.Items.ShouldHaveSingleItem().Name.ShouldBe("Kept");
        result.Warnings.ShouldBe([warning]);
    }

    [Fact]
    public void Read_password_protected_export_asks_for_the_file_password_by_file_name()
    {
        // Arrange
        string path = FormatSamples.WriteSample(DirectoryPath, "bitwarden-protected");
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

    [Fact]
    public void Read_account_restricted_export_throws_with_explanation()
    {
        // Arrange
        string path = FormatSamples.WriteSample(DirectoryPath, "bitwarden-restricted");
        var action = () => VaultFileReader.Read(path, _ => "unused");

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain("account-restricted", StringComparison.Ordinal);
    }

    [Fact]
    public void Read_unknown_format_throws_invalid_data()
    {
        // Arrange
        string path = FormatSamples.WriteSample(DirectoryPath, "other-text");
        var action = () => VaultFileReader.Read(path, _ => "unused");

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void Read_reports_an_input_that_is_a_directory_by_its_path()
    {
        // Arrange
        string path = Directory.CreateDirectory(Path.Combine(DirectoryPath, "export.json")).FullName;
        var action = () => VaultFileReader.Read(path, _ => "unused");

        // Act
        var exception = action.ShouldThrow<IOException>();

        // Assert
        exception.Message.ShouldContain(path, StringComparison.Ordinal);
        exception.Message.ShouldContain("is a directory", StringComparison.Ordinal);
    }

    [Fact]
    public void Read_rejects_files_larger_than_the_input_cap_before_reading()
    {
        // Arrange
        string path = Path.Combine(DirectoryPath, "large.json");
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

    [Fact]
    public void Read_zip_exports_return_items_from_the_archive()
    {
        // Arrange
        string keeper = FormatSamples.Write(DirectoryPath, "keeper.zip", TestZip.Create(
            ("export.json", TestZip.Utf8("""{"records":[{"uid":"u","title":"Zipped","login":"x"}]}"""))));

        // Act
        var result = VaultFileReader.Read(keeper, _ => "unused");

        // Assert
        result.Export.Items.ShouldHaveSingleItem().Type.ShouldBe(VaultItemType.Login);
    }
}
