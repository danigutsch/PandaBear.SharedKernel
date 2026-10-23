using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Formats;

public sealed class VaultFileReaderTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("vault-manager-read-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Theory]
    [InlineData("keeper-json", "keeper")]
    [InlineData("keeper-csv", "keeper")]
    [InlineData("bitwarden-json", "bitwarden")]
    [InlineData("bitwarden-csv", "bitwarden")]
    public void Read_dispatches_to_the_reader_for_the_detected_format(string sample, string provider)
    {
        string path = FormatSamples.WriteSample(directory, sample);

        var result = VaultFileReader.Read(path, _ => throw new InvalidOperationException("No password expected."));

        result.Export.Items.ShouldNotBeEmpty();
        result.Export.Items.ShouldAllSatisfy(item => item.Origin.Provider.ShouldBe(provider));
    }

    [Fact]
    public void Read_password_protected_export_asks_for_the_file_password_by_file_name()
    {
        string path = FormatSamples.WriteSample(directory, "bitwarden-protected");
        var asked = new List<string>();

        var result = VaultFileReader.Read(path, name =>
        {
            asked.Add(name);
            return "fixture password";
        });

        asked.ShouldBe(["protected.json"]);
        result.Export.Items.ShouldNotBeEmpty();
    }

    [Fact]
    public void Read_account_restricted_export_throws_with_explanation()
    {
        string path = FormatSamples.WriteSample(directory, "bitwarden-restricted");

        var action = () => VaultFileReader.Read(path, _ => "unused");

        var exception = action.ShouldThrow<InvalidDataException>();

        exception.Message.ShouldContain("account-restricted", StringComparison.Ordinal);
    }

    [Fact]
    public void Read_unknown_format_throws_invalid_data()
    {
        string path = FormatSamples.WriteSample(directory, "other-text");

        var action = () => VaultFileReader.Read(path, _ => "unused");

        action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void Read_reports_an_input_that_is_a_directory_by_its_path()
    {
        string path = Directory.CreateDirectory(Path.Combine(directory, "export.json")).FullName;

        var action = () => VaultFileReader.Read(path, _ => "unused");

        var exception = action.ShouldThrow<IOException>();
        exception.Message.ShouldContain(path, StringComparison.Ordinal);
        exception.Message.ShouldContain("is a directory", StringComparison.Ordinal);
    }

    [Fact]
    public void Read_rejects_files_larger_than_the_input_cap_before_reading()
    {
        string path = Path.Combine(directory, "large.json");
        using (var stream = File.Create(path))
        {
            stream.SetLength(VaultFileReader.MaxInputBytes + 1);
        }

        var action = () => VaultFileReader.Read(path, _ => "unused");

        var exception = action.ShouldThrow<InvalidDataException>();

        exception.Message.ShouldContain("too large", StringComparison.Ordinal);
    }

    [Fact]
    public void Read_zip_exports_return_items_from_the_archive()
    {
        string keeper = FormatSamples.Write(directory, "keeper.zip", TestZip.Create(
            ("export.json", TestZip.Utf8("""{"records":[{"uid":"u","title":"Zipped","login":"x"}]}"""))));

        var result = VaultFileReader.Read(keeper, _ => "unused");

        result.Export.Items.ShouldHaveSingleItem().Type.ShouldBe(VaultItemType.Login);
    }
}
