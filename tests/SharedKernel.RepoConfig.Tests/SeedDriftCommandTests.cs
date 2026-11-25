namespace SharedKernel.RepoConfig.Tests;

/// <summary>Tests the read-only comparison of scaffold seeds against the current catalog.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class SeedDriftCommandTests
{
    [Fact]
    public async Task Seed_drift_reports_each_selected_file_without_changing_the_repository()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var creation = await NewTestOperations.Run(
            workspace, "--name", "fixture-repository", "--language", "dotnet", "--sdk-version", "10.0.100", "--force");
        var agents = workspace.ReadFile("AGENTS.md").Replace("\n", "\r\n", StringComparison.Ordinal);
        var configuration = workspace.ReadFile(".config/sharedkernel-repo.json");
        const string edited = "Synthetic local settings.";
        workspace.WriteFile("AGENTS.md", agents);
        workspace.WriteFile("global.json", edited);
        workspace.DeleteFile("Directory.Build.props");

        // Act
        var (exitCode, output, error) = await NewTestOperations.SeedDrift(
            workspace, "--name", "fixture-repository", "--sdk-version", "10.0.100",
            "--feature", "build-props", "--feature", "nuget-config", "--feature", "agents", "--feature", "global-json");
        var baseline = await NewTestOperations.BaselineVerify(workspace);

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        exitCode.ShouldBe(1);
        error.ShouldBe(string.Empty);
        output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ShouldBe(
            ["current: AGENTS.md", "different: global.json", "current: NuGet.Config", "missing: Directory.Build.props"]);
        output.ShouldNotContain(edited, StringComparison.Ordinal);
        workspace.ReadFile("AGENTS.md").ShouldBe(agents);
        workspace.ReadFile("global.json").ShouldBe(edited);
        workspace.FileExists("Directory.Build.props").ShouldBeFalse();
        workspace.ReadFile(".config/sharedkernel-repo.json").ShouldBe(configuration);
        baseline.ExitCode.ShouldBe(0, baseline.Error);
    }

    [Fact]
    public async Task Seed_drift_accepts_a_matching_catalog_file_for_an_older_sdk()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var creation = await NewTestOperations.Run(
            workspace, "--language", "dotnet", "--sdk-version", "9.0.300", "--feature", "readme", "--feature", "global-json", "--force");

        // Act
        var (exitCode, output, error) = await NewTestOperations.SeedDrift(
            workspace, "--sdk-version", "9.0.300", "--feature", "global-json", "--feature", "global-json");

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldBe("current: global.json" + Environment.NewLine);
    }

    [Theory]
    [InlineData(new string[0], "select at least one --feature")]
    [InlineData(new[] { "--feature", "readme" }, "unsupported seed feature")]
    [InlineData(new[] { "--feature", "build-props" }, "needs --sdk-version")]
    [InlineData(new[] { "--feature", "global-json", "--sdk-version", "fixture" }, "invalid --sdk-version")]
    [InlineData(new[] { "--feature", "agents", "--name", "fixture/other" }, "invalid --name")]
    public async Task Seed_drift_rejects_invalid_render_inputs(string[] options, string expectedError)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var (exitCode, output, error) = await NewTestOperations.SeedDrift(workspace, options);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldContain(expectedError, StringComparison.Ordinal);
        Directory.EnumerateFileSystemEntries(workspace.RootPath).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, "is not valid UTF-8")]
    [InlineData(true, "exceeds the 1 MiB limit")]
    public async Task Seed_drift_reports_an_unreadable_seed_without_printing_its_content(bool oversized, string expectedError)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var path = Path.Combine(workspace.RootPath, "AGENTS.md");
        byte[] malformed = oversized ? new byte[(1024 * 1024) + 1] : [0xFF];
        await File.WriteAllBytesAsync(path, malformed, TestContext.Current.CancellationToken);

        // Act
        var (exitCode, output, error) = await NewTestOperations.SeedDrift(workspace, "--feature", "agents");
        var remaining = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldContain($"AGENTS.md {expectedError}", StringComparison.Ordinal);
        remaining.ShouldBe(malformed);
    }

    [Fact]
    public async Task Seed_drift_rejects_a_directory_in_place_of_a_seed()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var path = Path.Combine(workspace.RootPath, "AGENTS.md");
        Directory.CreateDirectory(path);

        // Act
        var (exitCode, output, error) = await NewTestOperations.SeedDrift(workspace, "--feature", "agents");

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldContain("AGENTS.md is a directory", StringComparison.Ordinal);
        Directory.Exists(path).ShouldBeTrue();
    }

    [Fact]
    public async Task Seed_drift_does_not_follow_a_symbolic_link_in_place_of_a_seed()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var outside = new TemporaryRepoConfigWorkspace();
        outside.WriteFile("external.txt", "Synthetic external text.");
        var path = Path.Combine(workspace.RootPath, "AGENTS.md");
        File.CreateSymbolicLink(path, Path.Combine(outside.RootPath, "external.txt"));

        // Act
        var (exitCode, output, error) = await NewTestOperations.SeedDrift(workspace, "--feature", "agents");

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldContain("Symbolic links are not followed", StringComparison.Ordinal);
        outside.ReadFile("external.txt").ShouldBe("Synthetic external text.");
    }
}
