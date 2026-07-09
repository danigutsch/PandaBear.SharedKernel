using SharedKernel.Versioning.Tool;

namespace SharedKernel.Versioning.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
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
            var solutionPath = Path.Combine(currentDirectory.FullName, "ViajantesTurismo.slnx");
            if (File.Exists(solutionPath))
            {
                repositoryRoot = currentDirectory.FullName;
                break;
            }

            currentDirectory = currentDirectory.Parent;
        }

        var root = (repositoryRoot).ShouldNotBeNull();
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
        properties["PackageId"].ShouldBe("SharedKernel.Versioning.Tool");
        properties["PackageReadmeFile"].ShouldBe("README.md");
        (readmeItem).ShouldNotBeNull();
    }

    [Fact]
    public static void SharedKernel_testing_helpers_are_packable_source_projects()
    {
        // Arrange
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        string? repositoryRoot = null;
        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "ViajantesTurismo.slnx");
            if (File.Exists(solutionPath))
            {
                repositoryRoot = currentDirectory.FullName;
                break;
            }

            currentDirectory = currentDirectory.Parent;
        }

        var root = (repositoryRoot).ShouldNotBeNull();
        string[] packageIds =
        [
            "SharedKernel.Testing",
            "SharedKernel.Testing.Assertions",
            "SharedKernel.Testing.Data",
            "SharedKernel.Testing.Http",
            "SharedKernel.Testing.Snapshots",
            "SharedKernel.Testing.Web",
            "SharedKernel.Testing.Roslyn",
            "SharedKernel.IntegrationTesting",
        ];

        foreach (var packageId in packageIds)
        {
            var projectPath = Path.Combine(root, "src", "SharedKernel", packageId, packageId + ".csproj");

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
            (readmeItem).ShouldNotBeNull();
        }
    }

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

    [Fact]
    public static void Parses_api_compatibility_options()
    {
        // Arrange
        string[] args =
        [
            "--version",
            "1.2.3",
            "--output-root",
            "artifacts/api",
            "--release-phase",
            "stable",
            "--repo-root",
            "/repo",
            "--baseline-version",
            "1.2.2",
            "--breaking-marker",
        ];

        // Act
        var options = ApiCompatibilityOptions.Parse(args);

        // Assert
        options.Version.ShouldBe("1.2.3");
        options.OutputRoot.ShouldBe("artifacts/api");
        options.ReleasePhase.ShouldBe("stable");
        options.RepoRoot.ShouldBe("/repo");
        options.BaselineVersion.ShouldBe("1.2.2");
        options.BreakingMarker.ShouldBeTrue();
    }

    [Fact]
    public static void Detects_breaking_change_markers()
    {
        // Arrange
        const string messages = "feat(api)!: remove contract\0fix: patch\0docs: update\n\nBREAKING CHANGE: docs contract\0";

        // Act
        var hasMarker = BreakingChangeMarkerCommand.HasMarker(messages);

        // Assert
        hasMarker.ShouldBeTrue();
    }

    [Fact]
    public static void Detects_breaking_change_markers_when_non_conventional_commits_are_present()
    {
        // Arrange
        const string messages = "Merge pull request #1 from branch\0feat(api)!: remove contract\0";

        // Act
        var hasMarker = BreakingChangeMarkerCommand.HasMarker(messages);

        // Assert
        hasMarker.ShouldBeTrue();
    }

    [Fact]
    public static void Ignores_non_conventional_commits_when_checking_breaking_change_markers()
    {
        // Arrange
        const string messages = "Merge pull request #1 from branch\0not a conventional commit\0";

        // Act
        var hasMarker = BreakingChangeMarkerCommand.HasMarker(messages);

        // Assert
        hasMarker.ShouldBeFalse();
    }

    [Fact]
    public static void Detects_breaking_dash_change_footer_in_non_conventional_commits()
    {
        // Arrange
        const string messages = "Merge pull request #1 from branch\n\nBREAKING-CHANGE: remove route\0";

        // Act
        var hasMarker = BreakingChangeMarkerCommand.HasMarker(messages);

        // Assert
        hasMarker.ShouldBeTrue();
    }

    [Fact]
    public static void Defines_api_compatibility_environment_variable_names()
    {
        // Assert
        ApiCompatibilityEnvironmentVariables.EnablePackageValidation.ShouldBe("API_COMPAT_ENABLE_PACKAGE_VALIDATION");
        ApiCompatibilityEnvironmentVariables.BaselineVersion.ShouldBe("API_COMPAT_BASELINE_VERSION");
    }

    [Fact]
    public static void Does_not_treat_stable_breaking_marker_as_report_only()
    {
        // Arrange
        var options = new ApiCompatibilityOptions(
            Version: "1.2.3",
            OutputRoot: "artifacts",
            ReleasePhase: "stable",
            RepoRoot: "/repo",
            BaselineVersion: "1.2.2",
            BreakingMarker: true);

        // Act
        var reportOnly = ApiCompatibilityCommand.ShouldTreatFailureAsReportOnly(options);

        // Assert
        reportOnly.ShouldBeFalse();
    }

    [Fact]
    public static void Treats_beta_breaking_marker_as_report_only()
    {
        // Arrange
        var options = new ApiCompatibilityOptions(
            Version: "1.2.3-beta.1",
            OutputRoot: "artifacts",
            ReleasePhase: "beta",
            RepoRoot: "/repo",
            BaselineVersion: "1.2.2",
            BreakingMarker: true);

        // Act
        var reportOnly = ApiCompatibilityCommand.ShouldTreatFailureAsReportOnly(options);

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

        var root = (repositoryRoot).ShouldNotBeNull();
        var props = File.ReadAllText(Path.Combine(root, "src", "Directory.Build.props"));

        // Act
        var usesProjectName = props.Contains("$(MSBuildProjectName)", StringComparison.Ordinal);

        // Assert
        usesProjectName.ShouldBeTrue();
        props.ShouldNotContain("src/SharedKernel/SharedKernel.");
    }

    [Fact]
    public static async Task Checks_public_api_baseline_files()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "SharedKernel", "SharedKernel.Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SharedKernel.Sample.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Shipped.txt"), "#nullable enable", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Unshipped.txt"), "#nullable enable", TestContext.Current.CancellationToken);

        using var output = new StringWriter();

        // Act
        await PublicApiBaselineCommand.Run(temporaryDirectory.Root, output);

        // Assert
        output.ToString().ShouldContain("Public API baselines", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Runs_check_public_api_baselines_command()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "SharedKernel", "SharedKernel.Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SharedKernel.Sample.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Shipped.txt"), "#nullable enable", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Unshipped.txt"), "#nullable enable", TestContext.Current.CancellationToken);
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(
            ["check-public-api-baselines", "--repo-root", temporaryDirectory.Root],
            input,
            output,
            error);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("Public API baselines", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Rejects_missing_sharedkernel_directory_for_public_api_baselines()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        // Act
        Func<Task> action = () => PublicApiBaselineCommand.Run(temporaryDirectory.Root, TextWriter.Null);

        // Assert
        var exception = await action.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("SharedKernel directory does not exist", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Rejects_sharedkernel_directory_without_projects_for_public_api_baselines()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        Directory.CreateDirectory(Path.Combine(temporaryDirectory.Root, "src", "SharedKernel"));
        // Act
        Func<Task> action = () => PublicApiBaselineCommand.Run(temporaryDirectory.Root, TextWriter.Null);

        // Assert
        var exception = await action.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("No SharedKernel projects found", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Restores_api_compatibility_environment_variables_after_report_only_failure()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "SharedKernel", "SharedKernel.Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SharedKernel.Sample.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>not-a-tfm</TargetFramework><PackageId>SharedKernel.Sample</PackageId></PropertyGroup></Project>",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Shipped.txt"), "#nullable enable", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Unshipped.txt"), "#nullable enable", TestContext.Current.CancellationToken);
        const string previousPackageValidation = "previous-validation";
        const string previousBaselineVersion = "previous-baseline";
        using var packageValidationScope = new EnvironmentVariableScope(ApiCompatibilityEnvironmentVariables.EnablePackageValidation, previousPackageValidation);
        using var baselineVersionScope = new EnvironmentVariableScope(ApiCompatibilityEnvironmentVariables.BaselineVersion, previousBaselineVersion);
        var options = new ApiCompatibilityOptions(
            Version: "1.2.3-alpha.1",
            OutputRoot: temporaryDirectory.OutputDirectory,
            ReleasePhase: "alpha",
            RepoRoot: temporaryDirectory.Root,
            BaselineVersion: "1.2.2",
            BreakingMarker: false);
        using var output = new StringWriter();

        // Act
        await ApiCompatibilityCommand.Run(options, output);

        // Assert
        output.ToString().ShouldContain("API compatibility report", StringComparison.Ordinal);
        Environment.GetEnvironmentVariable(ApiCompatibilityEnvironmentVariables.EnablePackageValidation).ShouldBe(previousPackageValidation);
        Environment.GetEnvironmentVariable(ApiCompatibilityEnvironmentVariables.BaselineVersion).ShouldBe(previousBaselineVersion);
    }

    [Fact]
    public static async Task Runs_api_compatibility_command_as_report_only_for_alpha_failures()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "SharedKernel", "SharedKernel.Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SharedKernel.Sample.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>not-a-tfm</TargetFramework><PackageId>SharedKernel.Sample</PackageId></PropertyGroup></Project>",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Shipped.txt"), "#nullable enable", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Unshipped.txt"), "#nullable enable", TestContext.Current.CancellationToken);
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(
            [
                "api-compatibility",
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
            input,
            output,
            error);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("API compatibility report", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_for_stable_api_compatibility_failure()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "SharedKernel", "SharedKernel.Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SharedKernel.Sample.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>not-a-tfm</TargetFramework><PackageId>SharedKernel.Sample</PackageId></PropertyGroup></Project>",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Shipped.txt"), "#nullable enable", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Unshipped.txt"), "#nullable enable", TestContext.Current.CancellationToken);
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(
            [
                "api-compatibility",
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
            input,
            output,
            error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("Error:", StringComparison.Ordinal);
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
    public static async Task Runs_has_breaking_change_marker_command_from_git_history()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        repository.Commit("file.txt", "base", "fix: base release");
        repository.Commit("file.txt", "next", "feat(api)!: remove route");
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(
            ["has-breaking-change-marker", "HEAD~1..HEAD", "--repo-root", repository.Root],
            input,
            output,
            error);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("Breaking-change marker found", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_when_breaking_change_marker_is_missing()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        repository.Commit("file.txt", "base", "fix: base release");
        repository.Commit("file.txt", "next", "feat(api): add route");
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(
            ["has-breaking-change-marker", "HEAD~1..HEAD", "--repo-root", repository.Root],
            input,
            output,
            error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("No breaking-change marker found", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_for_unknown_repo_root_option()
    {
        // Arrange
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(
            ["check-public-api-baselines", "--unknown", "value"],
            input,
            output,
            error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("Unknown repo-root option(s): --unknown value", StringComparison.Ordinal);
        error.ToString().ShouldContain("Expected: [--repo-root <path>]", StringComparison.Ordinal);
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
        releaseNotes.ShouldNotContain("Merge pull request");
        changelog.ShouldContain("# Changelog", StringComparison.Ordinal);
        manifest.ShouldContain("\"fileName\": \"SharedKernel.Results.1.2.3.nupkg\"", StringComparison.Ordinal);
        manifest.ShouldContain("\"sizeBytes\": 7", StringComparison.Ordinal);
        manifest.ShouldContain(
            "\"sha256\": \"BC4A71180870F7945155FBB02F4B0A2E3FAA2A62D6D31B7039013055ED19869A\"",
            StringComparison.Ordinal);
        manifest.ShouldContain("\"path\": \"sbom.spdx.json\"", StringComparison.Ordinal);
        manifest.ShouldContain("\"packageCount\": 1", StringComparison.Ordinal);
        attributions.ShouldContain("\"id\": \"Example.Package\"", StringComparison.Ordinal);
        attributions.ShouldContain("\"licenseExpression\": \"NOASSERTION\"", StringComparison.Ordinal);
        notices.ShouldContain("| `Example.Package` | `1.2.3` | `NOASSERTION` | yes |", StringComparison.Ordinal);
        sbom.ShouldContain("\"spdxVersion\": \"SPDX-2.3\"", StringComparison.Ordinal);
        sbom.ShouldContain("\"name\": \"Example.Package\"", StringComparison.Ordinal);
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
                <Authors>ViajantesTurismo contributors</Authors>
                <Company>ViajantesTurismo</Company>
                <Copyright>Copyright (c) 2025 ViajantesTurismo contributors</Copyright>
                <PackageLicenseExpression>MIT</PackageLicenseExpression>
                <RepositoryUrl>https://github.com/danigutsch/ViajantesTurismo</RepositoryUrl>
                <PublishRepositoryUrl>true</PublishRepositoryUrl>
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "SharedKernel", "SharedKernel.Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SharedKernel.Sample.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>SharedKernel.Sample</PackageId>
                <Description>Sample package.</Description>
                <PackageTags>sample;shared-kernel;dotnet</PackageTags>
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(
            ["validate-package-metadata", "--repo-root", temporaryDirectory.Root],
            input,
            output,
            error);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("Package metadata validation passed.", StringComparison.Ordinal);
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
                <Authors>ViajantesTurismo contributors</Authors>
                <Company />
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);
        var projectDirectory = Path.Combine(temporaryDirectory.Root, "src", "SharedKernel", "SharedKernel.Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SharedKernel.Sample.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>SharedKernel.Sample</PackageId>
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(
            ["validate-package-metadata", "--repo-root", temporaryDirectory.Root],
            input,
            output,
            error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("Package metadata validation failed:", StringComparison.Ordinal);
        error.ToString().ShouldContain("Directory.Build.props missing Company", StringComparison.Ordinal);
        error.ToString().ShouldContain("SharedKernel.Sample.csproj missing Description", StringComparison.Ordinal);
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
                <Authors>ViajantesTurismo contributors</Authors>
                <Company>ViajantesTurismo</Company>
                <Copyright>Copyright (c) 2025 ViajantesTurismo contributors</Copyright>
                <PackageLicenseExpression>MIT</PackageLicenseExpression>
                <RepositoryUrl>https://github.com/danigutsch/ViajantesTurismo</RepositoryUrl>
                <PublishRepositoryUrl>true</PublishRepositoryUrl>
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(
            ["validate-package-metadata", "--repo-root", temporaryDirectory.Root],
            input,
            output,
            error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("SharedKernel source directory does not exist:", StringComparison.Ordinal);
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

        var packagePath = Path.Combine(temporaryDirectory.PackageDirectory, "SharedKernel.Results.1.2.3.nupkg");
        await File.WriteAllBytesAsync(
            packagePath,
            "package"u8.ToArray(),
            TestContext.Current.CancellationToken);
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(
            [
                "prepare-release",
                "--version",
                "1.2.3",
                "--package-dir",
                temporaryDirectory.PackageDirectory,
                "--repo-root",
                temporaryDirectory.Root,
            ],
            input,
            output,
            error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("Invalid packages.lock.json:", StringComparison.Ordinal);
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

        var packagePath = Path.Combine(temporaryDirectory.PackageDirectory, "SharedKernel.Results.1.2.3.nupkg");
        await File.WriteAllBytesAsync(
            packagePath,
            "package"u8.ToArray(),
            TestContext.Current.CancellationToken);
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(
            [
                "prepare-release",
                "--version",
                "1.2.3",
                "--package-dir",
                temporaryDirectory.PackageDirectory,
                "--repo-root",
                temporaryDirectory.Root,
            ],
            input,
            output,
            error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("Package Example.Package", StringComparison.Ordinal);
        error.ToString().ShouldContain("has no resolved version.", StringComparison.Ordinal);
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

    [Fact]
    public static async Task Returns_error_for_api_compatibility_option_without_value()
    {
        // Arrange
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(["api-compatibility", "--version"], input, output, error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("--version must include a value", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_for_unknown_api_compatibility_option()
    {
        // Arrange
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(["api-compatibility", "--unknown"], input, output, error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("Unknown option '--unknown'", StringComparison.Ordinal);
    }

}
