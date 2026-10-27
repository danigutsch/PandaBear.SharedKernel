using PandaBear.VaultManager.Tool.Formats;

namespace PandaBear.VaultManager.Tool.Tests.Formats;

/// <summary>Tests that the format detector classifies vault files by content.</summary>
public sealed class FormatDetectorTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("vault-manager-detect-").FullName;

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    /// <summary>Each Keeper, Bitwarden, and unrelated sample file is detected as the expected format, with unrecognized zip, JSON, and text files detected as unknown.</summary>
    /// <param name="sample">The name of the sample written by the sample factory.</param>
    /// <param name="expected">The name of the expected format.</param>
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

    /// <summary>Keeper JSON content in a file named export.csv is still detected as Keeper JSON.</summary>
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
