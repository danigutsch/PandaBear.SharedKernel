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
    public static void Parses_calculate_release_options()
    {
        // Arrange
        string[] args =
        [
            "--repo-root",
            "/repo",
            "--version-kind",
            "stable",
            "--run-number",
            "42",
            "--sha",
            "abc123",
            "--github-output",
            "/tmp/out",
            "--github-summary",
            "/tmp/summary",
        ];

        // Act
        var options = CalculateReleaseOptions.Parse(args);

        // Assert
        options.RepoRoot.ShouldBe("/repo");
        options.VersionKind.ShouldBe("stable");
        options.RunNumber.ShouldBe("42");
        options.Sha.ShouldBe("abc123");
        options.GitHubOutput.ShouldBe("/tmp/out");
        options.GitHubSummary.ShouldBe("/tmp/summary");
    }

    [Fact]
    public static void Parses_pack_sharedkernel_options()
    {
        // Arrange
        string[] args =
        [
            "--version",
            "1.2.3",
            "--output-root",
            "artifacts",
            "--repo-root",
            "/repo",
            "--assembly-version",
            "1.0.0.0",
            "--file-version",
            "1.2.3.0",
            "--informational-version",
            "1.2.3+sha.abc123",
            "--skip-restore-check",
        ];

        // Act
        var options = PackSharedKernelOptions.Parse(args);

        // Assert
        options.Version.ShouldBe("1.2.3");
        options.OutputRoot.ShouldBe("artifacts");
        options.RepoRoot.ShouldBe("/repo");
        options.AssemblyVersion.ShouldBe("1.0.0.0");
        options.FileVersion.ShouldBe("1.2.3.0");
        options.InformationalVersion.ShouldBe("1.2.3+sha.abc123");
        options.VerifyRestore.ShouldBeFalse();
    }

    [Theory]
    [InlineData("beta")]
    [InlineData("production")]
    public static void Rejects_unknown_release_version_kind(string versionKind)
    {
        // Arrange
        string[] args = ["--version-kind", versionKind];

        // Act
        Action action = () => CalculateReleaseOptions.Parse(args);

        // Assert
        action.ShouldThrow<ArgumentException>().Message.ShouldContain("--version-kind", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SharedKernel.Results.1.2.3.snupkg")]
    [InlineData("SharedKernel.Results.1.2.3.symbols.nupkg")]
    public static async Task Rejects_existing_sharedkernel_package_variants(string fileName)
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var packageDirectory = Path.Combine(temporaryDirectory.PackageDirectory, "1.2.3");
        Directory.CreateDirectory(packageDirectory);
        await File.WriteAllBytesAsync(
            Path.Combine(packageDirectory, fileName),
            "package"u8.ToArray(),
            TestContext.Current.CancellationToken);
        var options = new PackSharedKernelOptions("1.2.3", temporaryDirectory.PackageDirectory, VerifyRestore: false, RepoRoot: temporaryDirectory.Root);

        // Act
        ArgumentException? exception = null;
        try
        {
            await SharedKernelPackCommand.Run(options, TextWriter.Null);
        }
        catch (ArgumentException ex)
        {
            exception = ex;
        }

        // Assert
        exception.ShouldNotBeNull();
        exception.Message.ShouldContain("Package version already exists", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Rejects_pack_sharedkernel_when_no_projects_exist()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        Directory.CreateDirectory(Path.Combine(temporaryDirectory.Root, "src", "SharedKernel"));
        var options = new PackSharedKernelOptions("1.2.3", "artifacts", VerifyRestore: false, RepoRoot: temporaryDirectory.Root);

        // Act
        ArgumentException? exception = null;
        try
        {
            await SharedKernelPackCommand.Run(options, TextWriter.Null);
        }
        catch (ArgumentException ex)
        {
            exception = ex;
        }

        // Assert
        exception.ShouldNotBeNull();
        exception.Message.ShouldContain("No SharedKernel projects found.", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Runs_pack_sharedkernel_command_with_relative_output_root()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "SharedKernel", "SharedKernel.Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SharedKernel.Sample.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <PackageId>SharedKernel.Sample</PackageId>
                <PackageVersion>$(ComputedSemVer)</PackageVersion>
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "Sample.cs"),
            "namespace SharedKernel.Sample; public sealed class Sample;",
            TestContext.Current.CancellationToken);

        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();
        string[] args =
        [
            "pack-sharedkernel",
            "--repo-root",
            temporaryDirectory.Root,
            "--version",
            "1.2.3",
            "--output-root",
            "artifacts/packages",
            "--skip-restore-check",
        ];

        // Act
        var exitCode = await VersioningToolApplication.Run(args, input, output, error);

        // Assert
        var packageDirectory = Path.Combine(temporaryDirectory.Root, "artifacts", "packages", "1.2.3");
        var packagePath = Path.Combine(packageDirectory, "SharedKernel.Sample.1.2.3.nupkg");
        exitCode.ShouldBe(0);
        File.Exists(packagePath).ShouldBeTrue();
        output.ToString().ShouldContain(packageDirectory, StringComparison.Ordinal);
    }

    [Fact]
    public static void Parses_prepare_release_options()
    {
        // Arrange
        string[] args =
        [
            "--version",
            "1.2.3",
            "--package-dir",
            "packages",
            "--output-dir",
            "release",
            "--source-tag",
            "v1.2.2",
            "--release-impact",
            "minor",
            "--sha",
            "abc123",
        ];

        // Act
        var options = PrepareReleaseOptions.Parse(args);

        // Assert
        options.Version.ShouldBe("1.2.3");
        options.PackageDirectory.ShouldBe("packages");
        options.OutputDirectory.ShouldBe("release");
        options.SourceTag.ShouldBe("v1.2.2");
        options.ReleaseImpact.ShouldBe("minor");
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
    public static async Task Runs_calculate_release_command_from_git_history()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        repository.Commit("file.txt", "base", "fix: base release");
        repository.Tag("v1.2.3");
        repository.Commit("file.txt", "next", "feat: add public capability");

        var outputPath = Path.Combine(temporaryDirectory.OutputDirectory, "github-output.txt");
        var summaryPath = Path.Combine(temporaryDirectory.OutputDirectory, "summary.md");
        Directory.CreateDirectory(temporaryDirectory.OutputDirectory);
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();
        string[] args =
        [
            "calculate-release",
            "--repo-root",
            repository.Root,
            "--version-kind",
            "prerelease",
            "--run-number",
            "7",
            "--sha",
            "abc123",
            "--github-output",
            outputPath,
            "--github-summary",
            summaryPath,
        ];

        // Act
        var exitCode = await VersioningToolApplication.Run(args, input, output, error);

        // Assert
        exitCode.ShouldBe(0);
        var json = output.ToString();
        var githubOutput = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
        var summary = await File.ReadAllTextAsync(summaryPath, TestContext.Current.CancellationToken);
        json.ShouldContain("\"packageVersion\":\"1.3.0-alpha.7\"", StringComparison.Ordinal);
        githubOutput.ShouldContain("source_tag=v1.2.3", StringComparison.Ordinal);
        githubOutput.ShouldContain("package_version=1.3.0-alpha.7", StringComparison.Ordinal);
        githubOutput.ShouldContain("release_impact=minor", StringComparison.Ordinal);
        summary.ShouldContain("- Base version: `1.2.3`", StringComparison.Ordinal);
    }

    [Fact]
    public static void Wraps_missing_process_start_failures()
    {
        // Arrange
        var executableName = "missing-sharedkernel-versioning-tool-command";

        // Act
        Action action = () => CommandRunner.Run(executableName, []);

        // Assert
        var exception = action.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("Failed to start " + executableName, StringComparison.Ordinal);
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

        using var input = new StringReader("- feat: add release prep (abc123)" + Environment.NewLine + "- Merge pull request #1 from branch");
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
        releaseNotes.ShouldNotContain("Merge pull request");
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
    public static async Task Returns_error_for_prepare_release_without_packages()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(
            ["prepare-release", "--version", "1.2.3", "--package-dir", temporaryDirectory.PackageDirectory],
            input,
            output,
            error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("No packages found", StringComparison.Ordinal);
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
