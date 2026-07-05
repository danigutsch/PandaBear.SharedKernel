using SharedKernel.Versioning.Tool;

namespace SharedKernel.Versioning.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class VersioningToolTests
{
    [Fact]
    public static void Parses_compute_options()
    {
        // Arrange
        string[] args = ["--base", "0.1.0", "--prerelease", "alpha.1", "--sha", "abc123"];

        // Act
        var options = VersionToolOptions.Parse(args);

        // Assert
        options.BaseVersion.ShouldBe("0.1.0");
        options.Prerelease.ShouldBe("alpha.1");
        options.Sha.ShouldBe("abc123");
    }

    [Fact]
    public static void Serializes_version_output_json()
    {
        // Arrange
        var output = VersionOutput.Create(new SemanticVersion(1, 2, 3), ReleaseImpact.Minor, "abc123");

        // Act
        var json = VersionOutputJson.Serialize(output);

        // Assert
        json.ShouldContain("\"semVer\":\"1.2.3\"", StringComparison.Ordinal);
        json.ShouldContain("\"releaseImpact\":\"minor\"", StringComparison.Ordinal);
        json.ShouldContain("\"assemblyVersion\":\"1.0.0.0\"", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Reads_null_separated_commit_messages_from_input()
    {
        // Arrange
        using var input = new StringReader("feat: add output\0fix: patch output");

        // Act
        var messages = await CommitMessageInput.ReadMessages(input);

        // Assert
        messages.ShouldContain("feat: add output");
        messages.ShouldContain("fix: patch output");
    }

    [Fact]
    public static async Task Runs_commit_impact_command()
    {
        // Arrange
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();
        string[] args = ["commit-impact", "feat(api)!:", "remove", "route"];

        // Act
        var exitCode = await VersioningToolApplication.Run(args, input, output, error);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("major", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Runs_compute_command_from_input()
    {
        // Arrange
        using var input = new StringReader("feat: add output\0fix: patch output");
        using var output = new StringWriter();
        using var error = new StringWriter();
        string[] args = ["compute", "--base", "0.1.0", "--prerelease", "alpha.1", "--sha", "abc123"];

        // Act
        var exitCode = await VersioningToolApplication.Run(args, input, output, error);

        // Assert
        exitCode.ShouldBe(0);
        var json = output.ToString();
        json.ShouldContain("\"semVer\":\"0.2.0-alpha.1\"", StringComparison.Ordinal);
        json.ShouldContain("\"informationalVersion\":\"0.2.0-alpha.1", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Runs_prepare_release_command_and_writes_artifacts()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();

        var packagePath = Path.Combine(temporaryDirectory.PackageDirectory, "SharedKernel.Results.1.2.3.nupkg");
        await File.WriteAllBytesAsync(
            packagePath,
            "package"u8.ToArray(),
            TestContext.Current.CancellationToken);

        using var input = new StringReader("- feat: add release prep (abc123)");
        using var output = new StringWriter();
        using var error = new StringWriter();
        string[] args =
        [
            "prepare-release",
            "--version",
            "1.2.3",
            "--package-dir",
            temporaryDirectory.PackageDirectory,
            "--output-dir",
            temporaryDirectory.OutputDirectory,
            "--source-tag",
            "v1.2.2",
            "--release-impact",
            "minor",
            "--sha",
            "abc123",
        ];

        // Act
        var exitCode = await VersioningToolApplication.Run(args, input, output, error);

        // Assert
        exitCode.ShouldBe(0);
        var releaseNotes = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "release-notes.md"),
            TestContext.Current.CancellationToken);
        var changelog = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "CHANGELOG.md"),
            TestContext.Current.CancellationToken);
        var manifest = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "release-manifest.json"),
            TestContext.Current.CancellationToken);

        releaseNotes.ShouldContain("# Release 1.2.3", StringComparison.Ordinal);
        releaseNotes.ShouldContain("- Commit: `abc123`", StringComparison.Ordinal);
        releaseNotes.ShouldContain("- Previous release tag: `v1.2.2`", StringComparison.Ordinal);
        releaseNotes.ShouldContain("- Release impact: `minor`", StringComparison.Ordinal);
        releaseNotes.ShouldContain("- feat: add release prep (abc123)", StringComparison.Ordinal);
        changelog.ShouldContain("# Changelog", StringComparison.Ordinal);
        manifest.ShouldContain("\"fileName\": \"SharedKernel.Results.1.2.3.nupkg\"", StringComparison.Ordinal);
        manifest.ShouldContain("\"sizeBytes\": 7", StringComparison.Ordinal);
        manifest.ShouldContain(
            "\"sha256\": \"BC4A71180870F7945155FBB02F4B0A2E3FAA2A62D6D31B7039013055ED19869A\"",
            StringComparison.Ordinal);
        manifest.ShouldContain("\"sbom\": null", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_for_missing_prepare_release_options()
    {
        // Arrange
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(["prepare-release"], input, output, error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("--version is required.", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_for_missing_prepare_release_package_directory()
    {
        // Arrange
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(
            ["prepare-release", "--version", "1.2.3", "--package-dir", "/path/not/found"],
            input,
            output,
            error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("Package directory does not exist:", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Prints_help_for_prepare_release_help_option()
    {
        // Arrange
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(["prepare-release", "--help"], input, output, error);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("prepare-release", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_usage_for_unknown_command()
    {
        // Arrange
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(["unknown"], input, output, error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("Usage:", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Prints_help_for_help_option()
    {
        // Arrange
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(["--help"], input, output, error);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("Usage:", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Prints_version_for_version_option()
    {
        // Arrange
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(["--version"], input, output, error);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldNotBe(string.Empty);
    }

    [Fact]
    public static async Task Returns_error_for_invalid_commit_impact_message()
    {
        // Arrange
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(["commit-impact", "invalid"], input, output, error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("Error:", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_for_invalid_compute_options()
    {
        // Arrange
        using var input = new StringReader("feat: add output");
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(["compute", "--base", "not-a-version"], input, output, error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("Error:", StringComparison.Ordinal);
    }

}
