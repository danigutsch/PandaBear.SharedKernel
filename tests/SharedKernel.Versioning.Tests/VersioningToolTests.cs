using PandaBear.Testing;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using PandaBear.Versioning.Tool;

namespace PandaBear.Versioning.Tests;

[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class VersioningToolTests
{
    [Fact]
    public static void Tool_project_is_packaged_as_dotnet_tool()
    {
        // Arrange
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        string? repositoryRoot = null;
        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "SharedKernel.slnx");
            if (File.Exists(solutionPath))
            {
                repositoryRoot = currentDirectory.FullName;
                break;
            }

            currentDirectory = currentDirectory.Parent;
        }

        var root = repositoryRoot.ShouldNotBeNull();
        var projectPath = Path.Combine(
            root,
            "tools",
            "SharedKernel.Versioning.Tool",
            "SharedKernel.Versioning.Tool.csproj");

        // Act
        var project = System.Xml.Linq.XDocument.Load(projectPath);
        var properties = project.Descendants().Where(element => element.Parent?.Name.LocalName == "PropertyGroup").ToDictionary(
            element => element.Name.LocalName,
            element => element.Value);
        var readmeItem = project.Descendants("None").SingleOrDefault(element =>
            string.Equals((string?)element.Attribute("Include"), "README.md", StringComparison.Ordinal));

        // Assert
        properties["PackAsTool"].ShouldBe("true");
        properties["ToolCommandName"].ShouldBe("sharedkernel-version");
        properties["PackageId"].ShouldBe("PandaBear.SharedKernel.Versioning.Tool");
        properties["PackageReadmeFile"].ShouldBe("README.md");
        readmeItem.ShouldNotBeNull();
    }

    [Fact]
    public static void SharedKernel_testing_helpers_are_packable_source_projects()
    {
        // Arrange
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        string? repositoryRoot = null;
        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "SharedKernel.slnx");
            if (File.Exists(solutionPath))
            {
                repositoryRoot = currentDirectory.FullName;
                break;
            }

            currentDirectory = currentDirectory.Parent;
        }

        var root = repositoryRoot.ShouldNotBeNull();
        string[] packageIds =
        [
            "PandaBear.SharedKernel.Testing",
            "PandaBear.SharedKernel.Testing.Assertions",
            "PandaBear.SharedKernel.Testing.Data",
            "PandaBear.SharedKernel.Testing.Http",
            "PandaBear.SharedKernel.Testing.Snapshots",
            "PandaBear.SharedKernel.Testing.Web",
            "PandaBear.SharedKernel.Testing.Roslyn",
            "PandaBear.SharedKernel.IntegrationTesting",
        ];

        foreach (var packageId in packageIds)
        {
            var projectName = packageId["PandaBear.".Length..];
            var projectPath = Path.Combine(root, "src", "SharedKernel", projectName, projectName + ".csproj");

            // Act
            var project = System.Xml.Linq.XDocument.Load(projectPath);
            var properties = project.Descendants().Where(element => element.Parent?.Name.LocalName == "PropertyGroup").ToDictionary(
                element => element.Name.LocalName,
                element => element.Value);
            var readmeItem = project.Descendants("None").SingleOrDefault(element =>
                string.Equals((string?)element.Attribute("Include"), "README.md", StringComparison.Ordinal));

            // Assert
            properties["PackageId"].ShouldBe(packageId);
            properties["PackageReadmeFile"].ShouldBe("README.md");
            properties["IsAotCompatible"].ShouldBe("false");
            readmeItem.ShouldNotBeNull();
        }
    }

    [Fact]
    public static void Detects_breaking_change_markers()
    {
        // Arrange
        const string messages = "feat(api)!: remove contract\0fix: patch\0docs: update\n\nBREAKING CHANGE: docs contract\0";

        // Act
        var hasMarker = BreakingChangeMarker.HasMarker(messages);

        // Assert
        hasMarker.ShouldBeTrue();
    }

    [Fact]
    public static void Detects_breaking_change_markers_when_non_conventional_commits_are_present()
    {
        // Arrange
        const string messages = "Merge pull request #1 from branch\0feat(api)!: remove contract\0";

        // Act
        var hasMarker = BreakingChangeMarker.HasMarker(messages);

        // Assert
        hasMarker.ShouldBeTrue();
    }

    [Fact]
    public static void Ignores_non_conventional_commits_when_checking_breaking_change_markers()
    {
        // Arrange
        const string messages = "Merge pull request #1 from branch\0not a conventional commit\0";

        // Act
        var hasMarker = BreakingChangeMarker.HasMarker(messages);

        // Assert
        hasMarker.ShouldBeFalse();
    }

    [Fact]
    public static void Detects_breaking_dash_change_footer_in_non_conventional_commits()
    {
        // Arrange
        const string messages = "Merge pull request #1 from branch\n\nBREAKING-CHANGE: remove route\0";

        // Act
        var hasMarker = BreakingChangeMarker.HasMarker(messages);

        // Assert
        hasMarker.ShouldBeTrue();
    }

    [Fact]
    public static void Does_not_treat_stable_breaking_marker_as_report_only()
    {
        // Arrange
        var options = new ApiCompatibilityReportOptions(
            Version: "1.2.3",
            OutputRoot: "artifacts",
            ReleasePhase: "stable",
            RepoRoot: "/repo",
            BaselineVersion: "1.2.2",
            BreakingMarker: true);

        // Act
        var reportOnly = ApiCompatibilityReport.ShouldTreatFailureAsReportOnly(options);

        // Assert
        reportOnly.ShouldBeFalse();
    }

    [Fact]
    public static void Treats_beta_breaking_marker_as_report_only()
    {
        // Arrange
        var options = new ApiCompatibilityReportOptions(
            Version: "1.2.3-beta.1",
            OutputRoot: "artifacts",
            ReleasePhase: "beta",
            RepoRoot: "/repo",
            BaselineVersion: "1.2.2",
            BreakingMarker: true);

        // Act
        var reportOnly = ApiCompatibilityReport.ShouldTreatFailureAsReportOnly(options);

        // Assert
        reportOnly.ShouldBeTrue();
    }

    [Fact]
    public static void SharedKernel_analyzer_condition_uses_project_name()
    {
        // Arrange
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        string? repositoryRoot = null;
        while (currentDirectory is not null)
        {
            var propsPath = Path.Combine(currentDirectory.FullName, "src", "Directory.Build.props");
            if (File.Exists(propsPath))
            {
                repositoryRoot = currentDirectory.FullName;
                break;
            }

            currentDirectory = currentDirectory.Parent;
        }

        var root = repositoryRoot.ShouldNotBeNull();
        var props = File.ReadAllText(Path.Combine(root, "src", "Directory.Build.props"));

        // Act
        var usesProjectName = props.Contains("$(MSBuildProjectName)", StringComparison.Ordinal);

        // Assert
        usesProjectName.ShouldBeTrue();
        props.ShouldNotContain("src/SharedKernel/SharedKernel.", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Checks_public_API_baseline_files()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "SharedKernel", "PandaBear.Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SharedKernel.Sample.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Shipped.txt"), "#nullable enable", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Unshipped.txt"), "#nullable enable", TestContext.Current.CancellationToken);

        // Act
        var checkedProjects = PublicApiBaselineChecker.EnsureBaselinesPresent(temporaryDirectory.Root);

        // Assert
        checkedProjects.ShouldBe(1);
    }

    [Fact]
    public static async Task Runs_check_public_API_baselines_command()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "SharedKernel", "PandaBear.Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SharedKernel.Sample.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Shipped.txt"), "#nullable enable", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Unshipped.txt"), "#nullable enable", TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["check-public-api-baselines", "--repo-root", temporaryDirectory.Root],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("Public API baselines", StringComparison.Ordinal);
    }

    [Fact]
    public static void Rejects_missing_sharedkernel_directory_for_public_API_baselines()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        // Act
        Action action = () => PublicApiBaselineChecker.EnsureBaselinesPresent(temporaryDirectory.Root);

        // Assert
        var exception = action.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("SharedKernel directory does not exist", StringComparison.Ordinal);
    }

    [Fact]
    public static void Rejects_sharedkernel_directory_without_projects_for_public_API_baselines()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        Directory.CreateDirectory(Path.Combine(temporaryDirectory.Root, "src", "SharedKernel"));
        // Act
        Action action = () => PublicApiBaselineChecker.EnsureBaselinesPresent(temporaryDirectory.Root);

        // Assert
        var exception = action.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("No SharedKernel projects found", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Check_API_compatibility_validates_packages_against_the_baseline_version()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        await temporaryDirectory.WriteUnpackableSharedKernelProject(TestContext.Current.CancellationToken);
        string[] args =
        [
            "check-api-compatibility",
            "--repo-root",
            temporaryDirectory.Root,
            "--output-root",
            temporaryDirectory.OutputDirectory,
            "--version",
            "1.2.3-alpha.1",
            "--baseline-version",
            "1.2.2",
        ];

        // Act
        var result = await VersioningCommandLineRunner.Invoke(args, TestContext.Current.CancellationToken);

        // Assert
        var report = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "1.2.3-alpha.1", "api-compatibility-report.md"),
            TestContext.Current.CancellationToken);
        result.ExitCode.ShouldBe(0);
        report.ShouldContain("-p:EnablePackageValidation=true", StringComparison.Ordinal);
        report.ShouldContain("-p:PackageValidationBaselineVersion=1.2.2", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Check_API_compatibility_defaults_the_version_to_a_local_alpha_timestamp()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        await temporaryDirectory.WriteUnpackableSharedKernelProject(TestContext.Current.CancellationToken);
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        string[] args = ["check-api-compatibility", "--repo-root", temporaryDirectory.Root, "--output-root", temporaryDirectory.OutputDirectory];

        // Act
        var result = await VersioningCommandLineRunner.Invoke(args, string.Empty, clock, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain(
            Path.Combine(temporaryDirectory.OutputDirectory, "0.1.0-alpha.local.20260102030405", "api-compatibility-report.md"),
            StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Runs_check_API_compatibility_command_as_report_only_for_alpha_failures()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        await temporaryDirectory.WriteUnpackableSharedKernelProject(TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [
                "check-api-compatibility",
                "--repo-root",
                temporaryDirectory.Root,
                "--output-root",
                temporaryDirectory.OutputDirectory,
                "--version",
                "1.2.3-alpha.1",
                "--release-phase",
                "alpha",
                "--baseline-version",
                "1.2.2",
            ],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("API compatibility report", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 2)]
    public static async Task Check_API_compatibility_reports_beta_failures_only_with_a_breaking_marker(bool breakingMarker, int expectedExitCode)
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        await temporaryDirectory.WriteUnpackableSharedKernelProject(TestContext.Current.CancellationToken);
        string[] marker = breakingMarker ? ["--breaking-marker"] : [];
        string[] args =
        [
            "check-api-compatibility",
            "--repo-root",
            temporaryDirectory.Root,
            "--output-root",
            temporaryDirectory.OutputDirectory,
            "--version",
            "1.2.3-beta.1",
            "--release-phase",
            "beta",
            .. marker,
        ];

        // Act
        var result = await VersioningCommandLineRunner.Invoke(args, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(expectedExitCode);
        File.Exists(Path.Combine(temporaryDirectory.OutputDirectory, "1.2.3-beta.1", "api-compatibility-report.md")).ShouldBeTrue();
    }

    [Fact]
    public static async Task Returns_error_for_stable_API_compatibility_failure()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        await temporaryDirectory.WriteUnpackableSharedKernelProject(TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [
                "check-api-compatibility",
                "--repo-root",
                temporaryDirectory.Root,
                "--output-root",
                temporaryDirectory.OutputDirectory,
                "--version",
                "1.2.3",
                "--release-phase",
                "stable",
                "--baseline-version",
                "1.2.2",
                "--breaking-marker",
            ],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldContain("Error:", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("PandaBear.SharedKernel.Results.1.2.3.snupkg")]
    [InlineData("PandaBear.SharedKernel.Results.1.2.3.symbols.nupkg")]
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
        Func<Task> act = () => SharedKernelPackCommand.Run(options, TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        var exception = await act.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("Package version already exists", StringComparison.Ordinal);
    }

    [Fact]
    public static void Rejects_pack_project_discovery_when_sharedkernel_directory_is_missing()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();

        // Act
        Action action = () => SharedKernelPackagePlanner.FindProjects(temporaryDirectory.Root);

        // Assert
        var exception = action.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("SharedKernel directory does not exist", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Rejects_pack_sharedkernel_when_no_projects_exist()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        Directory.CreateDirectory(Path.Combine(temporaryDirectory.Root, "src", "SharedKernel"));
        var options = new PackSharedKernelOptions("1.2.3", "artifacts", VerifyRestore: false, RepoRoot: temporaryDirectory.Root);

        // Act
        Func<Task> act = () => SharedKernelPackCommand.Run(options, TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        var exception = await act.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("No SharedKernel projects found.", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Runs_pack_sharedkernel_command_with_relative_output_root()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "SharedKernel", "PandaBear.Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SharedKernel.Sample.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <PackageId>PandaBear.SharedKernel.Sample</PackageId>
                <PackageVersion>$(ComputedSemVer)</PackageVersion>
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "Sample.cs"),
            "namespace PandaBear.Sample; public sealed class Sample;",
            TestContext.Current.CancellationToken);

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
        var result = await VersioningCommandLineRunner.Invoke(args, TestContext.Current.CancellationToken);

        // Assert
        var packageDirectory = Path.Combine(temporaryDirectory.Root, "artifacts", "packages", "1.2.3");
        var packagePath = Path.Combine(packageDirectory, "PandaBear.SharedKernel.Sample.1.2.3.nupkg");
        result.ExitCode.ShouldBe(0);
        File.Exists(packagePath).ShouldBeTrue();
        var normalizedOutput = result.Output.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        normalizedOutput.ShouldContain(packageDirectory, StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Pack_sharedkernel_passes_version_options_to_dotnet_pack()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        await temporaryDirectory.WriteUnpackableSharedKernelProject(TestContext.Current.CancellationToken);
        string[] args =
        [
            "pack-sharedkernel",
            "--repo-root",
            temporaryDirectory.Root,
            "--version",
            "1.2.3",
            "--assembly-version",
            "1.0.0.0",
            "--file-version",
            "1.2.3.4",
            "--informational-version",
            "1.2.3+sha.abc123",
            "--skip-restore-check",
        ];

        // Act
        var result = await VersioningCommandLineRunner.Invoke(args, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("-p:ComputedSemVer=1.2.3 ", StringComparison.Ordinal);
        result.Error.ShouldContain("-p:ComputedAssemblyVersion=1.0.0.0", StringComparison.Ordinal);
        result.Error.ShouldContain("-p:ComputedFileVersion=1.2.3.4", StringComparison.Ordinal);
        result.Error.ShouldContain("-p:ComputedInformationalVersion=1.2.3+sha.abc123", StringComparison.Ordinal);
        result.Error.ShouldNotContain("EnablePackageValidation", StringComparison.Ordinal);
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
    public static async Task Runs_classify_commit_command()
    {
        // Arrange
        string[] args = ["classify-commit", "feat(api)!:", "remove", "route"];

        // Act
        var result = await VersioningCommandLineRunner.Invoke(args, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldBe("major" + Environment.NewLine);
        result.Error.ShouldBe(string.Empty);
    }

    [Fact]
    public static async Task Runs_compute_command_from_input()
    {
        // Arrange
        string[] args = ["compute", "--base", "0.1.0", "--prerelease", "alpha.1", "--sha", "abc123"];

        // Act
        var result = await VersioningCommandLineRunner.Invoke(args, "feat: add output\0fix: patch output", TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        var json = result.Output;
        json.ShouldContain("\"semVer\":\"0.2.0-alpha.1\"", StringComparison.Ordinal);
        json.ShouldContain("\"informationalVersion\":\"0.2.0-alpha.1+sha.abc123\"", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Runs_calculate_release_command_from_git_history()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        await repository.Initialize(TestContext.Current.CancellationToken);
        await repository.Commit("file.txt", "base", "fix: base release", TestContext.Current.CancellationToken);
        await repository.Tag("v1.2.3", TestContext.Current.CancellationToken);
        await repository.Commit("file.txt", "next", "feat: add public capability", TestContext.Current.CancellationToken);

        var outputPath = Path.Combine(temporaryDirectory.OutputDirectory, "github-output.txt");
        var summaryPath = Path.Combine(temporaryDirectory.OutputDirectory, "summary.md");
        Directory.CreateDirectory(temporaryDirectory.OutputDirectory);
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
        var result = await VersioningCommandLineRunner.Invoke(args, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        var json = result.Output;
        var githubOutput = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
        var summary = await File.ReadAllTextAsync(summaryPath, TestContext.Current.CancellationToken);
        json.ShouldContain("\"packageVersion\":\"1.3.0-alpha.7\"", StringComparison.Ordinal);
        json.ShouldContain("\"informationalVersion\":\"1.3.0-alpha.7+sha.abc123\"", StringComparison.Ordinal);
        githubOutput.ShouldContain("source_tag=v1.2.3", StringComparison.Ordinal);
        githubOutput.ShouldContain("package_version=1.3.0-alpha.7", StringComparison.Ordinal);
        githubOutput.ShouldContain("release_impact=minor", StringComparison.Ordinal);
        summary.ShouldContain("- Base version: `1.2.3`", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Runs_has_breaking_change_marker_command_from_git_history()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        await repository.Commit("file.txt", "base", "fix: base release", TestContext.Current.CancellationToken);
        await repository.Commit("file.txt", "next", "feat(api)!: remove route", TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["has-breaking-change-marker", "HEAD~1..HEAD", "--repo-root", repository.Root],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("Breaking-change marker found", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_when_breaking_change_marker_is_missing()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        await repository.Commit("file.txt", "base", "fix: base release", TestContext.Current.CancellationToken);
        await repository.Commit("file.txt", "next", "feat(api): add route", TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["has-breaking-change-marker", "HEAD~1..HEAD", "--repo-root", repository.Root],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("No breaking-change marker found", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Breaking_change_marker_check_passes_the_range_to_git_after_the_end_of_options()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        await repository.Commit("file.txt", "base", "feat(api)!: remove route", TestContext.Current.CancellationToken);
        var root = repository.Root;

        // Act
        // Parsing rejects such a range; the command still keeps Git from reading it as an option.
        var action = () => BreakingChangeMarkerCommand.Run("--output=log.txt", root, TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        await action.ShouldThrow<InvalidOperationException>();
        File.Exists(Path.Combine(root, "log.txt")).ShouldBeFalse();
    }

    [Fact]
    public static async Task Wraps_missing_process_start_failures()
    {
        // Arrange
        var executableName = "missing-sharedkernel-versioning-tool-command";

        // Act
        Func<Task> action = () => CommandRunner.Run(executableName, [], null, TestContext.Current.CancellationToken);

        // Assert
        var exception = await action.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("Failed to start " + executableName, StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Canceled_runs_do_not_start_processes()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> action = () => CommandRunner.Run("missing-sharedkernel-versioning-tool-command", [], null, cancellation.Token);

        // Assert
        await action.ShouldThrow<OperationCanceledException>();
    }

    [Fact]
    public static async Task Cancellation_terminates_the_running_process()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var processIdPath = Path.Combine(directory.Path, "child.pid");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = CommandRunner.Run(
            "/bin/sh",
            ["-c", "echo $$ > child.pid.tmp && mv child.pid.tmp child.pid && exec sleep 60"],
            directory.Path,
            cancellation.Token);
        while (!File.Exists(processIdPath) && !running.IsCompleted)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        }

        var processId = int.Parse(await File.ReadAllTextAsync(processIdPath, TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);

        // Act
        await cancellation.CancelAsync();
        var action = () => running.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await action.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        var lookup = () => Process.GetProcessById(processId);
        lookup.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public static async Task Termination_ignores_a_process_object_without_a_running_process()
    {
        // Arrange
        using Process process = new();

        // Act
        var exception = await Record.ExceptionAsync(() => CommandRunner.Terminate(process));

        // Assert
        exception.ShouldBeNull();
    }

    [Fact]
    public static async Task Termination_stops_waiting_when_a_killed_process_never_reports_its_exit()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var unreaped = await UnreapedProcess.Start(TestContext.Current.CancellationToken);

        // Act
        await CommandRunner.Terminate(unreaped.Process).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Assert
        unreaped.IsZombie.ShouldBeTrue();
        unreaped.Process.HasExited.ShouldBeFalse();
    }

    [Fact]
    public static async Task Writes_release_preparation_artifacts()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(temporaryDirectory.Root, "Directory.Build.props"),
            """
            <Project>
              <PropertyGroup>
                <RepositoryUrl>https://example.invalid/custom-repo</RepositoryUrl>
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "packages.lock.json"),
            """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Example.Package": {
                    "type": "Direct",
                    "requested": "[1.2.3, )",
                    "resolved": "1.2.3",
                    "contentHash": "abc"
                  }
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);

        var packagePath = Path.Combine(temporaryDirectory.PackageDirectory, "PandaBear.SharedKernel.Results.1.2.3.nupkg");
        await File.WriteAllBytesAsync(
            packagePath,
            "package"u8.ToArray(),
            TestContext.Current.CancellationToken);

        var options = new ReleasePreparationOptions(
            "1.2.3",
            temporaryDirectory.PackageDirectory,
            temporaryDirectory.OutputDirectory,
            temporaryDirectory.Root,
            "v1.2.2",
            "minor",
            "abc123");
        var changes = "- feat: add release prep (abc123)" + Environment.NewLine + "- Merge pull request #1 from branch";

        // Act
        await ReleasePreparationArtifacts.Write(options, changes);

        // Assert
        var releaseNotes = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "release-notes.md"),
            TestContext.Current.CancellationToken);
        var changelog = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "CHANGELOG.md"),
            TestContext.Current.CancellationToken);
        var manifest = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "release-manifest.json"),
            TestContext.Current.CancellationToken);
        var attributions = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "third-party-attributions.json"),
            TestContext.Current.CancellationToken);
        var notices = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "third-party-notices.md"),
            TestContext.Current.CancellationToken);
        var sbom = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "sbom.spdx.json"),
            TestContext.Current.CancellationToken);

        releaseNotes.ShouldContain("# Release 1.2.3", StringComparison.Ordinal);
        releaseNotes.ShouldContain("- Commit: `abc123`", StringComparison.Ordinal);
        releaseNotes.ShouldContain("- Previous release tag: `v1.2.2`", StringComparison.Ordinal);
        releaseNotes.ShouldContain("- Release impact: `minor`", StringComparison.Ordinal);
        releaseNotes.ShouldContain("- feat: add release prep (abc123)", StringComparison.Ordinal);
        releaseNotes.ShouldNotContain("Merge pull request", StringComparison.Ordinal);
        changelog.ShouldContain("# Changelog", StringComparison.Ordinal);
        manifest.ShouldContain("\"fileName\": \"PandaBear.SharedKernel.Results.1.2.3.nupkg\"", StringComparison.Ordinal);
        manifest.ShouldContain("\"sizeBytes\": 7", StringComparison.Ordinal);
        manifest.ShouldContain(
            "\"sha256\": \"BC4A71180870F7945155FBB02F4B0A2E3FAA2A62D6D31B7039013055ED19869A\"",
            StringComparison.Ordinal);
        manifest.ShouldContain("\"path\": \"sbom.spdx.json\"", StringComparison.Ordinal);
        manifest.ShouldContain("\"packageCount\": 1", StringComparison.Ordinal);
        manifest.ShouldContain("\"jsonPath\": \"third-party-attributions.json\"", StringComparison.Ordinal);
        manifest.ShouldContain("\"noticePath\": \"third-party-notices.md\"", StringComparison.Ordinal);
        manifest.ShouldContain("\"licenseEvidenceNote\":", StringComparison.Ordinal);
        manifest.ShouldNotContain("sbomNote", StringComparison.Ordinal);
        manifest.ShouldNotContain(temporaryDirectory.OutputDirectory, StringComparison.Ordinal);
        attributions.ShouldContain("\"id\": \"Example.Package\"", StringComparison.Ordinal);
        attributions.ShouldContain("\"licenseExpression\": \"NOASSERTION\"", StringComparison.Ordinal);
        notices.ShouldContain("| `Example.Package` | `1.2.3` | `NOASSERTION` | yes |", StringComparison.Ordinal);
        sbom.ShouldContain("\"spdxVersion\": \"SPDX-2.3\"", StringComparison.Ordinal);
        sbom.ShouldContain("\"documentNamespace\": \"https://example.invalid/custom-repo/sbom/1.2.3\"", StringComparison.Ordinal);
        sbom.ShouldContain("\"name\": \"Example.Package\"", StringComparison.Ordinal);
        using var manifestJson = JsonDocument.Parse(manifest);
        using var attributionsJson = JsonDocument.Parse(attributions);
        using var sbomJson = JsonDocument.Parse(sbom);
        var manifestRoot = manifestJson.RootElement;
        var attributionsRoot = attributionsJson.RootElement;
        var sbomRoot = sbomJson.RootElement;
        var thirdPartyAttributions = manifestRoot.GetProperty("thirdPartyAttributions");
        var attributionJsonPath = thirdPartyAttributions.GetProperty("jsonPath").GetString().ShouldNotBeNull();
        var attributionNoticePath = thirdPartyAttributions.GetProperty("noticePath").GetString().ShouldNotBeNull();
        var licenseEvidenceNote = manifestRoot.GetProperty("licenseEvidenceNote").GetString().ShouldNotBeNull();
        var documentNamespace = sbomRoot.GetProperty("documentNamespace").GetString().ShouldNotBeNull();
        attributionJsonPath.ShouldBe("third-party-attributions.json");
        attributionNoticePath.ShouldBe("third-party-notices.md");
        licenseEvidenceNote.ShouldContain("license fields use NOASSERTION", StringComparison.Ordinal);
        documentNamespace.ShouldBe("https://example.invalid/custom-repo/sbom/1.2.3");
        attributionsRoot.GetProperty("packages").GetArrayLength().ShouldBe(1);
        sbomRoot.GetProperty("packages").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public static void Writes_reproducible_spdx_created_timestamp_from_source_date_epoch()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        using var sourceDateEpochScope = new EnvironmentVariableScope("SOURCE_DATE_EPOCH", "946684800");
        var options = new ReleasePreparationOptions(
            "1.2.3",
            temporaryDirectory.PackageDirectory,
            temporaryDirectory.OutputDirectory,
            temporaryDirectory.Root,
            SourceTag: null,
            ReleaseImpact: null,
            Sha: null);

        // Act
        var sbom = PackageLockInventory.WriteSpdxJson(options, []);

        // Assert
        sbom.ShouldContain("\"created\": \"2000-01-01T00:00:00Z\"", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-a-timestamp", "SOURCE_DATE_EPOCH must be a Unix timestamp in seconds.")]
    [InlineData("9223372036854775807", "SOURCE_DATE_EPOCH is outside the supported Unix timestamp range.")]
    public static async Task Returns_error_for_invalid_source_date_epoch(string sourceDateEpoch, string expectedMessage)
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        using var sourceDateEpochScope = new EnvironmentVariableScope("SOURCE_DATE_EPOCH", sourceDateEpoch);
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "packages.lock.json"),
            """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Example.Package": {
                    "type": "Direct",
                    "requested": "[1.2.3, )",
                    "resolved": "1.2.3"
                  }
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(temporaryDirectory.PackageDirectory, "PandaBear.SharedKernel.Results.1.2.3.nupkg"),
            "package"u8.ToArray(),
            TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [
                "prepare-release",
                "--version",
                "1.2.3",
                "--package-dir",
                temporaryDirectory.PackageDirectory,
                "--repo-root",
                temporaryDirectory.Root,
            ],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(expectedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Writes_spdx_json_when_repository_url_property_uses_msbuild_casing()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(temporaryDirectory.Root, "Directory.Build.props"),
            """
            <Project>
              <PropertyGroup>
                <RepositoryURL>https://example.invalid/cased-repo</RepositoryURL>
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);
        var options = new ReleasePreparationOptions(
            "1.2.3",
            temporaryDirectory.PackageDirectory,
            temporaryDirectory.OutputDirectory,
            temporaryDirectory.Root,
            SourceTag: null,
            ReleaseImpact: null,
            Sha: null);

        // Act
        var sbom = PackageLockInventory.WriteSpdxJson(options, []);

        // Assert
        using var sbomJson = JsonDocument.Parse(sbom);
        var documentNamespace = sbomJson.RootElement.GetProperty("documentNamespace").GetString().ShouldNotBeNull();
        documentNamespace.ShouldBe("https://example.invalid/cased-repo/sbom/1.2.3");
    }

    [Fact]
    public static async Task Reads_package_lock_inventory_from_src_only()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var sourceProjectDirectory = Path.Combine(temporaryDirectory.Root, "src", "Sample");
        var testProjectDirectory = Path.Combine(temporaryDirectory.Root, "tests", "Sample.Tests");
        Directory.CreateDirectory(sourceProjectDirectory);
        Directory.CreateDirectory(testProjectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(sourceProjectDirectory, "packages.lock.json"),
            """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Example.Package": {
                    "type": "Direct",
                    "requested": "[1.2.3, )",
                    "resolved": "1.2.3"
                  }
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(testProjectDirectory, "packages.lock.json"),
            """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "TestOnly.Package": {
                    "type": "Direct",
                    "requested": "[9.9.9, )",
                    "resolved": "9.9.9"
                  }
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);

        // Act
        var packages = PackageLockInventory.Read(temporaryDirectory.Root);

        // Assert
        var package = packages.ShouldHaveSingleItem();
        package.Id.ShouldBe("Example.Package");
        package.Version.ShouldBe("1.2.3");
        package.LockFiles.ShouldContain("src/Sample/packages.lock.json");
        package.LockFiles.ShouldNotContain("tests/Sample.Tests/packages.lock.json");
    }

    [Fact]
    public static async Task Reads_package_lock_inventory_in_deterministic_order()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var firstProjectDirectory = Path.Combine(temporaryDirectory.Root, "src", "Sample.A");
        var secondProjectDirectory = Path.Combine(temporaryDirectory.Root, "src", "Sample.B");
        Directory.CreateDirectory(firstProjectDirectory);
        Directory.CreateDirectory(secondProjectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(secondProjectDirectory, "packages.lock.json"),
            """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Zeta.Package": {
                    "type": "Transitive",
                    "resolved": "2.0.0"
                  },
                  "Example.Package": {
                    "type": "Direct",
                    "resolved": "2.0.0"
                  }
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(firstProjectDirectory, "packages.lock.json"),
            """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Example.Package": {
                    "type": "Direct",
                    "resolved": "1.0.0"
                  },
                  "Alpha.Package": {
                    "type": "Transitive",
                    "resolved": "3.0.0"
                  }
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);

        // Act
        var packages = PackageLockInventory.Read(temporaryDirectory.Root);

        // Assert
        packages.Select(package => package.Id + "@" + package.Version).ShouldBe(
            [
                "Alpha.Package@3.0.0",
                "Example.Package@1.0.0",
                "Example.Package@2.0.0",
                "Zeta.Package@2.0.0",
            ]);
        packages[1].LockFiles.ShouldBe(["src/Sample.A/packages.lock.json"]);
        packages[2].LockFiles.ShouldBe(["src/Sample.B/packages.lock.json"]);
    }

    [Fact]
    public static async Task Ignores_generated_package_locks_under_source_directory()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "Sample");
        var objDirectory = Path.Combine(projectDirectory, "obj");
        var binDirectory = Path.Combine(projectDirectory, "bin");
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(objDirectory);
        Directory.CreateDirectory(binDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "packages.lock.json"),
            """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Maintained.Package": {
                    "type": "Direct",
                    "resolved": "1.0.0"
                  }
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(objDirectory, "packages.lock.json"),
            """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Generated.Obj.Package": {
                    "type": "Direct",
                    "resolved": "9.0.0"
                  }
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(binDirectory, "packages.lock.json"),
            """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Generated.Bin.Package": {
                    "type": "Direct",
                    "resolved": "9.0.0"
                  }
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);

        // Act
        var packages = PackageLockInventory.Read(temporaryDirectory.Root);

        // Assert
        var package = packages.ShouldHaveSingleItem();
        package.Id.ShouldBe("Maintained.Package");
        package.LockFiles.ShouldBe(["src/Sample/packages.lock.json"]);
    }

    [Fact]
    public static async Task Validates_package_metadata_for_packable_sharedkernel_projects()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(temporaryDirectory.Root, "Directory.Build.props"),
            """
            <Project>
              <PropertyGroup>
                <Authors>PandaBear contributors</Authors>
                <Company>PandaBear</Company>
                <Copyright>Copyright (c) 2025 PandaBear contributors</Copyright>
                <PackageLicenseExpression>MIT</PackageLicenseExpression>
                <RepositoryUrl>https://github.com/danigutsch/PandaBear.SharedKernel</RepositoryUrl>
                <PublishRepositoryUrl>true</PublishRepositoryUrl>
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "SharedKernel", "PandaBear.Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SharedKernel.Sample.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>PandaBear.SharedKernel.Sample</PackageId>
                <Description>Sample package.</Description>
                <PackageTags>sample;shared-kernel;dotnet</PackageTags>
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["validate-package-metadata", "--repo-root", temporaryDirectory.Root],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("Package metadata validation passed.", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Validates_package_metadata_with_msbuild_property_casing()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(temporaryDirectory.Root, "Directory.Build.props"),
            """
            <Project>
              <PropertyGroup>
                <Authors>PandaBear contributors</Authors>
                <Company>PandaBear</Company>
                <Copyright>Copyright (c) 2025 PandaBear contributors</Copyright>
                <PackageLicenseExpression>MIT</PackageLicenseExpression>
                <RepositoryURL>https://github.com/danigutsch/PandaBear.SharedKernel</RepositoryURL>
                <PublishRepositoryURL>true</PublishRepositoryURL>
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "SharedKernel", "PandaBear.Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SharedKernel.Sample.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageID>PandaBear.Sample</PackageID>
                <Description>Sample package.</Description>
                <PackageTags>sample;shared-kernel;dotnet</PackageTags>
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["validate-package-metadata", "--repo-root", temporaryDirectory.Root],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("Package metadata validation passed.", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_for_missing_package_metadata()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(temporaryDirectory.Root, "Directory.Build.props"),
            """
            <Project>
              <PropertyGroup>
                <Authors>PandaBear contributors</Authors>
                <Company />
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "SharedKernel", "PandaBear.Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SharedKernel.Sample.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>PandaBear.SharedKernel.Sample</PackageId>
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["validate-package-metadata", "--repo-root", temporaryDirectory.Root],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Package metadata validation failed:", StringComparison.Ordinal);
        result.Error.ShouldContain("Directory.Build.props missing Company", StringComparison.Ordinal);
        result.Error.ShouldContain("SharedKernel.Sample.csproj missing Description", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_for_invalid_project_property_file()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(temporaryDirectory.Root, "Directory.Build.props"),
            "<Project><PropertyGroup>",
            TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["validate-package-metadata", "--repo-root", temporaryDirectory.Root],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Invalid project property file:", StringComparison.Ordinal);
        result.Error.ShouldContain("Directory.Build.props", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_when_sharedkernel_source_directory_is_missing_for_package_metadata()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(temporaryDirectory.Root, "Directory.Build.props"),
            """
            <Project>
              <PropertyGroup>
                <Authors>PandaBear contributors</Authors>
                <Company>PandaBear</Company>
                <Copyright>Copyright (c) 2025 PandaBear contributors</Copyright>
                <PackageLicenseExpression>MIT</PackageLicenseExpression>
                <RepositoryUrl>https://github.com/danigutsch/PandaBear.SharedKernel</RepositoryUrl>
                <PublishRepositoryUrl>true</PublishRepositoryUrl>
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["validate-package-metadata", "--repo-root", temporaryDirectory.Root],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("SharedKernel source directory does not exist:", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_for_invalid_package_lock_inventory()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "packages.lock.json"),
            "{ invalid json",
            TestContext.Current.CancellationToken);

        var packagePath = Path.Combine(temporaryDirectory.PackageDirectory, "PandaBear.SharedKernel.Results.1.2.3.nupkg");
        await File.WriteAllBytesAsync(
            packagePath,
            "package"u8.ToArray(),
            TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [
                "prepare-release",
                "--version",
                "1.2.3",
                "--package-dir",
                temporaryDirectory.PackageDirectory,
                "--repo-root",
                temporaryDirectory.Root,
            ],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Invalid packages.lock.json:", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_for_package_lock_without_dependencies_section()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "packages.lock.json"),
            """
            {
              "version": 2
            }
            """,
            TestContext.Current.CancellationToken);

        var packagePath = Path.Combine(temporaryDirectory.PackageDirectory, "PandaBear.SharedKernel.Results.1.2.3.nupkg");
        await File.WriteAllBytesAsync(
            packagePath,
            "package"u8.ToArray(),
            TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [
                "prepare-release",
                "--version",
                "1.2.3",
                "--package-dir",
                temporaryDirectory.PackageDirectory,
                "--repo-root",
                temporaryDirectory.Root,
            ],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Unexpected packages.lock.json schema:", StringComparison.Ordinal);
        result.Error.ShouldContain("Missing top-level dependencies section.", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_when_no_source_package_locks_are_found()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        Directory.CreateDirectory(Path.Combine(temporaryDirectory.Root, "src"));
        var packagePath = Path.Combine(temporaryDirectory.PackageDirectory, "PandaBear.SharedKernel.Results.1.2.3.nupkg");
        await File.WriteAllBytesAsync(
            packagePath,
            "package"u8.ToArray(),
            TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [
                "prepare-release",
                "--version",
                "1.2.3",
                "--package-dir",
                temporaryDirectory.PackageDirectory,
                "--repo-root",
                temporaryDirectory.Root,
            ],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("No packages.lock.json files found under source directory:", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_for_package_lock_without_resolved_version()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "packages.lock.json"),
            """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Example.Package": {
                    "type": "Direct",
                    "requested": "[1.2.3, )",
                    "resolved": " "
                  }
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);

        var packagePath = Path.Combine(temporaryDirectory.PackageDirectory, "PandaBear.SharedKernel.Results.1.2.3.nupkg");
        await File.WriteAllBytesAsync(
            packagePath,
            "package"u8.ToArray(),
            TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [
                "prepare-release",
                "--version",
                "1.2.3",
                "--package-dir",
                temporaryDirectory.PackageDirectory,
                "--repo-root",
                temporaryDirectory.Root,
            ],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Package Example.Package", StringComparison.Ordinal);
        result.Error.ShouldContain("has no resolved version.", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Runs_prepare_release_command_and_writes_artifacts()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "packages.lock.json"),
            """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Example.Package": {
                    "type": "Direct",
                    "requested": "[1.2.3, )",
                    "resolved": "1.2.3"
                  }
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);

        var packagePath = Path.Combine(temporaryDirectory.PackageDirectory, "PandaBear.SharedKernel.Results.1.2.3.nupkg");
        await File.WriteAllBytesAsync(
            packagePath,
            "package"u8.ToArray(),
            TestContext.Current.CancellationToken);

        string[] args =
        [
            "prepare-release",
            "--version",
            "1.2.3",
            "--package-dir",
            temporaryDirectory.PackageDirectory,
            "--output-dir",
            temporaryDirectory.OutputDirectory,
            "--repo-root",
            temporaryDirectory.Root,
            "--source-tag",
            "v1.2.2",
            "--release-impact",
            "minor",
            "--sha",
            "abc123",
        ];

        // Act
        var result = await VersioningCommandLineRunner.Invoke(args, "- feat: add release prep (abc123)", TestContext.Current.CancellationToken);

        // Assert
        var releaseNotes = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "release-notes.md"),
            TestContext.Current.CancellationToken);
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldBe("Release prep artifacts: " + temporaryDirectory.OutputDirectory + Environment.NewLine);
        releaseNotes.ShouldContain("- Commit: `abc123`", StringComparison.Ordinal);
        releaseNotes.ShouldContain("- Previous release tag: `v1.2.2`", StringComparison.Ordinal);
        releaseNotes.ShouldContain("- Release impact: `minor`", StringComparison.Ordinal);
        releaseNotes.ShouldContain("- feat: add release prep (abc123)", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_for_missing_prepare_release_package_directory()
    {
        // Arrange

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["prepare-release", "--version", "1.2.3", "--package-dir", "/path/not/found"],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Package directory does not exist:", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_for_prepare_release_without_packages()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "packages.lock.json"),
            """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Example.Package": {
                    "type": "Direct",
                    "requested": "[1.2.3, )",
                    "resolved": "1.2.3"
                  }
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [
                "prepare-release",
                "--version",
                "1.2.3",
                "--package-dir",
                temporaryDirectory.PackageDirectory,
                "--repo-root",
                temporaryDirectory.Root,
            ],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("No packages found", StringComparison.Ordinal);
    }

}
