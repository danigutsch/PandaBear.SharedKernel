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
        // Arrange
        string path = FormatSamples.WriteSample(directory, sample);

        // Act
        var format = FormatDetector.Detect(path);

        // Assert
        format.ShouldBe(Enum.Parse<VaultFileFormat>(expected));
    }

    [Fact]
    public void Detect_ignores_file_extension()
    {
        // Arrange
        string path = FormatSamples.Write(directory, "export.csv", Fixture.Read("keeper/web.json"));

        // Act
        var format = FormatDetector.Detect(path);

        // Assert
        format.ShouldBe(VaultFileFormat.KeeperJson);
    }
}
