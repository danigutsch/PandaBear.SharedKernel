using static PandaBear.RepoConfig.Tests.NewTestOperations;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the <c>new</c> command.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class NewCommandTests
{
    [Fact]
    public async Task New_previews_every_creation_and_writes_nothing_without_force()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var (exitCode, output, error) = await Run(workspace, "--name", "example-repo");

        // Assert
        exitCode.ShouldBe(0, "A preview succeeds.");
        error.ShouldBe(string.Empty);
        output.ShouldContain("preview: would create README.md.", StringComparison.Ordinal);
        output.ShouldContain("preview: would create .config/sharedkernel-repo.json.", StringComparison.Ordinal);
        output.ShouldContain("preview: would run git init -b main.", StringComparison.Ordinal);
        output.ShouldContain("--force writes them", StringComparison.Ordinal, "The preview says how to write.");
        workspace.FileExists("README.md").ShouldBeFalse("A preview writes nothing.");
        Directory.Exists(Path.Combine(workspace.RootPath, ".git")).ShouldBeFalse("A preview initializes nothing.");
    }

    [Fact]
    public async Task New_with_force_writes_a_repository_that_baseline_verify_accepts()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var (exitCode, _, error) = await Run(workspace, "--name", "example-repo", "--branch", "trunk", "--force");
        var verification = await BaselineVerify(workspace);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        workspace.ReadFile("README.md").ShouldContain("# example-repo", StringComparison.Ordinal, "The name variable is substituted.");
        workspace.ReadFile("README.md").ShouldContain("`trunk`", StringComparison.Ordinal, "The branch variable is substituted.");
        workspace.FileExists(".gitignore").ShouldBeTrue();
        workspace.FileExists(".gitattributes").ShouldBeTrue();
        workspace.FileExists(".editorconfig").ShouldBeTrue();
        workspace.FileExists(".githooks/pre-commit").ShouldBeTrue("The hooks feature writes the tracked hook.");
        Directory.Exists(Path.Combine(workspace.RootPath, ".git")).ShouldBeTrue("A repository is initialized.");
        await GitConfigShouldBe(workspace, "core.hooksPath", ".githooks");
        await GitConfigShouldBe(workspace, "pull.ff", "only");
        await GitConfigShouldBe(workspace, "init.defaultBranch", "trunk");
        await GitConfigShouldBe(workspace, "commit.gpgsign", string.Empty, "Signing is opt-in.");
        verification.ExitCode.ShouldBe(0, "The written configuration lets baseline verify pass.");
        verification.Error.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task New_creates_a_missing_root_directory_and_defaults_the_name_to_its_last_segment()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var root = Path.Combine(workspace.RootPath, "nested", "my-project");

        // Act
        var (exitCode, _, error) = await RunAt(root, "--force");

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        var readme = await File.ReadAllTextAsync(Path.Combine(root, "README.md"), TestContext.Current.CancellationToken);
        readme.ShouldContain("# my-project", StringComparison.Ordinal);
    }

    [Fact]
    public async Task New_with_the_dotnet_language_writes_the_dotnet_gitignore()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var (exitCode, _, error) = await Run(workspace, "--name", "example-repo", "--language", "dotnet", "--sdk-version", "10.0.100", "--force");

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        workspace.ReadFile(".gitignore").ShouldContain("[Bb]in/", StringComparison.Ordinal, "The dotnet language selects the .NET ignore rules.");
        var verification = await BaselineVerify(workspace);
        verification.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task New_with_features_writes_only_the_selected_features()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var (exitCode, _, error) = await Run(workspace, "--name", "example-repo", "--feature", "readme", "--feature", "signed-commits", "--force");

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        workspace.FileExists("README.md").ShouldBeTrue();
        workspace.FileExists(".gitignore").ShouldBeFalse("An unselected feature writes nothing.");
        workspace.FileExists(".githooks/pre-commit").ShouldBeFalse();
        await GitConfigShouldBe(workspace, "commit.gpgsign", "true");
        await GitConfigShouldBe(workspace, "tag.gpgsign", "true");
        await GitConfigShouldBe(workspace, "gpg.format", "ssh");
        await GitConfigShouldBe(workspace, "pull.ff", string.Empty, "git-config was not selected.");
    }

    [Theory]
    [InlineData("--language", "cobol", "language")]
    [InlineData("--feature", "magic", "feature")]
    public async Task New_rejects_an_unknown_language_or_feature_and_lists_the_valid_ones(string option, string value, string noun)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var (exitCode, output, error) = await Run(workspace, "--name", "example-repo", option, value, "--force");

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldContain($"unknown {noun} '{value}'", StringComparison.Ordinal, "The error names the bad value.");
        error.ShouldContain(noun == "language" ? "dotnet" : "readme", StringComparison.Ordinal, "The error lists valid IDs.");
        workspace.FileExists("README.md").ShouldBeFalse("Nothing is written.");
    }

    [Fact]
    public async Task New_rejects_an_invalid_repository_name()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var (exitCode, _, error) = await Run(workspace, "--name", "bad name!", "--force");

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("--name", StringComparison.Ordinal, "The shared baseline validation is reported with the option name.");
        workspace.FileExists("README.md").ShouldBeFalse();
    }

    [Fact]
    public async Task New_fails_without_writing_when_a_selected_file_exists_unmanaged_even_in_a_preview()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("README.md", "hand written\n");

        // Act
        var (exitCode, _, error) = await Run(workspace, "--name", "example-repo");

        // Assert
        exitCode.ShouldBe(1, "A preview that would fail under --force does not exit 0.");
        error.ShouldContain("conflict: README.md", StringComparison.Ordinal);
        workspace.ReadFile("README.md").ShouldBe("hand written\n", "The existing file is untouched.");
        workspace.FileExists(".gitignore").ShouldBeFalse("Nothing else is written either.");
    }

    [Fact]
    public async Task New_fails_when_the_repository_already_has_a_baseline_configuration()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".config/sharedkernel-repo.json", """{ "version": 1 }""");

        // Act
        var (exitCode, _, error) = await Run(workspace, "--name", "example-repo", "--force");

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("already exists", StringComparison.Ordinal);
        error.ShouldContain("baseline apply", StringComparison.Ordinal, "The error points at the command for existing repositories.");
        workspace.FileExists("README.md").ShouldBeFalse();
    }

    [Fact]
    public async Task New_run_twice_with_force_fails_the_second_time_and_changes_nothing()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        await Run(workspace, "--name", "example-repo", "--force");
        var before = workspace.ReadFile("README.md");

        // Act
        var (exitCode, _, error) = await Run(workspace, "--name", "example-repo", "--force");

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("already exists", StringComparison.Ordinal);
        workspace.ReadFile("README.md").ShouldBe(before);
    }

    [Fact]
    public async Task New_with_the_dotnet_language_writes_the_seed_files_once_and_verify_still_passes()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var (exitCode, _, error) = await Run(workspace, "--name", "example-repo", "--language", "dotnet", "--sdk-version", "10.0.100", "--force");
        var verification = await BaselineVerify(workspace);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        var globalJson = workspace.ReadFile("global.json");
        globalJson.ShouldContain("\"version\": \"10.0.100\"", StringComparison.Ordinal, "The SDK version is pinned.");
        globalJson.ShouldContain("\"rollForward\": \"disable\"", StringComparison.Ordinal, "The pin is exact.");
        globalJson.ShouldContain("Microsoft.Testing.Platform", StringComparison.Ordinal, "SDK 10 and later get the test runner setting.");
        workspace.ReadFile("NuGet.Config").ShouldContain("packageSourceMapping", StringComparison.Ordinal, "Package source mapping is on.");
        workspace.ReadFile("Directory.Build.props").ShouldContain("<TargetFramework>net10.0</TargetFramework>", StringComparison.Ordinal, "The target framework follows the SDK major version.");
        workspace.ReadFile("AGENTS.md").ShouldContain("example-repo", StringComparison.Ordinal, "The name is substituted.");
        verification.ExitCode.ShouldBe(0, "Seed files are not managed, so verify ignores them.");
    }

    [Fact]
    public async Task New_omits_the_test_runner_setting_for_an_sdk_before_10()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var (exitCode, _, _) = await Run(workspace, "--name", "example-repo", "--language", "dotnet", "--sdk-version", "9.0.300", "--force");

        // Assert
        exitCode.ShouldBe(0);
        workspace.ReadFile("global.json").ShouldNotContain("Microsoft.Testing.Platform", StringComparison.Ordinal, "The setting needs SDK 10.");
        workspace.ReadFile("Directory.Build.props").ShouldContain("net9.0", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(new[] { "--language", "dotnet" }, "needs --sdk-version")]
    [InlineData(new[] { "--language", "dotnet", "--sdk-version", "ten" }, "invalid --sdk-version")]
    [InlineData(new[] { "--language", "dotnet", "--sdk-version", "5.0.100" }, "invalid --sdk-version")]
    [InlineData(new[] { "--feature", "global-json", "--sdk-version", "10.0.100" }, "needs --language dotnet")]
    public async Task New_rejects_a_missing_or_invalid_sdk_version_and_a_dotnet_feature_without_the_language(string[] options, string expected)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var (exitCode, output, error) = await Run(workspace, [.. options, "--name", "example-repo", "--force"]);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldContain(expected, StringComparison.Ordinal);
        workspace.FileExists("README.md").ShouldBeFalse("Nothing is written.");
    }

    [Fact]
    public async Task New_fails_without_writing_when_a_seed_file_already_exists()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("AGENTS.md", "mine\n");

        // Act
        var (exitCode, _, error) = await Run(workspace, "--name", "example-repo", "--force");

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("conflict: AGENTS.md", StringComparison.Ordinal);
        workspace.ReadFile("AGENTS.md").ShouldBe("mine\n", "The existing file is untouched.");
        workspace.FileExists("README.md").ShouldBeFalse("Nothing else is written either.");
    }

    [Fact]
    public async Task New_treats_a_git_file_as_an_existing_repository_and_does_not_initialize_again()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".git", "gitdir: ../elsewhere\n");

        // Act
        var (exitCode, output, _) = await Run(workspace, "--name", "example-repo", "--feature", "readme");

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldNotContain("git init", StringComparison.Ordinal, "A worktree's .git file marks an existing repository.");
    }
}
