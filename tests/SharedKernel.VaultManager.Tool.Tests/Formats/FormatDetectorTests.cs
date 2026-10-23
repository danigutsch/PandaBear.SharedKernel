using PandaBear.VaultManager.Tool.Formats;

namespace PandaBear.VaultManager.Tool.Tests.Formats;

public sealed class FormatDetectorTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("vault-manager-detect-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Theory]
    [InlineData("keeper-json", "KeeperJson")]
    [InlineData("keeper-array", "KeeperJson")]
    [InlineData("keeper-zip", "KeeperJsonZip")]
    [InlineData("keeper-csv", "KeeperCsv")]
    [InlineData("bitwarden-json", "BitwardenJson")]
    [InlineData("bitwarden-protected", "BitwardenJsonPasswordProtected")]
    [InlineData("bitwarden-restricted", "BitwardenJsonAccountRestricted")]
    [InlineData("bitwarden-csv", "BitwardenCsv")]
    [InlineData("bitwarden-org-csv", "BitwardenCsv")]
    [InlineData("bitwarden-zip", "BitwardenZip")]
    [InlineData("other-zip", "Unknown")]
    [InlineData("other-json", "Unknown")]
    [InlineData("other-text", "Unknown")]
    public void Detect_identifies_format_by_content(string sample, string expected)
    {
        string path = FormatSamples.WriteSample(directory, sample);

        var format = FormatDetector.Detect(path);

        format.ShouldBe(Enum.Parse<VaultFileFormat>(expected));
    }

    [Fact]
    public void Detect_ignores_file_extension()
    {
        string path = FormatSamples.Write(directory, "export.csv", Fixture.Read("keeper/web.json"));

        FormatDetector.Detect(path).ShouldBe(VaultFileFormat.KeeperJson);
    }
}
