using System.Xml.Linq;
using static SharedKernel.RepoConfig.Tests.NewTestOperations;

namespace SharedKernel.RepoConfig.Tests;

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
        workspace.FileExists("LICENSE").ShouldBeFalse();
        workspace.FileExists("SECURITY.md").ShouldBeFalse();
        workspace.FileExists("CODEOWNERS").ShouldBeFalse();
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
    public async Task New_renders_consumer_policy_sources_with_caller_supplied_values()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var sources = new TemporaryRepoConfigWorkspace();
        sources.WriteFile("license.txt", "Copyright @@copyrightHolder@@\n");
        sources.WriteFile("security.txt", "# Security for @@repositoryName@@\nContact @@owner@@\n");
        sources.WriteFile("owners.txt", "* @@@owner@@\n");
        var ownersSource = Path.GetRelativePath(Path.GetTempPath(), Path.Combine(sources.RootPath, "owners.txt"));

        // Act
        var (exitCode, output, error) = await Run(
            workspace,
            "--name", "fixture-repository",
            "--feature", "readme",
            "--license-source", Path.Combine(sources.RootPath, "license.txt"),
            "--security-source", Path.Combine(sources.RootPath, "security.txt"),
            "--codeowners-source", ownersSource,
            "--owner", "fixture-owner",
            "--copyright-holder", "Fixture Rights Holder",
            "--force");
        var verification = await BaselineVerify(workspace);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        workspace.ReadFile("LICENSE").ShouldBe("Copyright Fixture Rights Holder\n");
        workspace.ReadFile("SECURITY.md").ShouldBe("# Security for fixture-repository\nContact fixture-owner\n");
        workspace.ReadFile("CODEOWNERS").ShouldBe("* @fixture-owner\n");
        workspace.FileExists(".gitignore").ShouldBeFalse("Exact feature selection stays in force.");
        var configuration = workspace.ReadFile(".config/sharedkernel-repo.json");
        configuration.ShouldNotContain("fixture-owner", StringComparison.Ordinal);
        configuration.ShouldNotContain("Fixture Rights Holder", StringComparison.Ordinal);
        configuration.ShouldNotContain("license.txt", StringComparison.Ordinal);
        output.ShouldNotContain("Fixture Rights Holder", StringComparison.Ordinal);
        verification.ExitCode.ShouldBe(0);
    }

    [Theory]
    [InlineData("--license-source", "LICENSE")]
    [InlineData("--security-source", "SECURITY.md")]
    [InlineData("--codeowners-source", "CODEOWNERS")]
    public async Task New_reports_a_missing_consumer_source_without_writing(string sourceOption, string destination)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var missingSource = Path.Combine(workspace.RootPath, "missing-source.txt");

        // Act
        var (exitCode, output, error) = await Run(workspace, sourceOption, missingSource, "--force");

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain($"{destination}: source", StringComparison.Ordinal);
        error.ShouldContain("missing-source.txt", StringComparison.Ordinal);
        error.ShouldContain("does not exist", StringComparison.Ordinal);
        output.ShouldBe(string.Empty);
        workspace.FileExists("README.md").ShouldBeFalse();
        workspace.FileExists(".config/sharedkernel-repo.json").ShouldBeFalse();
        Directory.Exists(Path.Combine(workspace.RootPath, ".git")).ShouldBeFalse();
    }

    [Fact]
    public async Task New_previews_a_policy_source_and_creates_no_policy_file()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("license-template.txt", "Consumer-supplied text.\n");
        var source = Path.Combine(workspace.RootPath, "license-template.txt");

        // Act
        var (exitCode, output, error) = await Run(workspace, "--license-source", source);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldContain("preview: would create LICENSE.", StringComparison.Ordinal);
        output.ShouldNotContain("Consumer-supplied text.", StringComparison.Ordinal);
        workspace.FileExists("LICENSE").ShouldBeFalse();
    }

    [Theory]
    [InlineData("@@owner@@", "--owner")]
    [InlineData("@@copyrightHolder@@", "--copyright-holder")]
    public async Task New_reports_a_missing_policy_variable_before_writing(string template, string option)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("license-template.txt", template);
        var source = Path.Combine(workspace.RootPath, "license-template.txt");

        // Act
        var (exitCode, _, error) = await Run(workspace, "--license-source", source, "--force");

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("LICENSE", StringComparison.Ordinal);
        error.ShouldContain($"requires {option}", StringComparison.Ordinal);
        workspace.FileExists("LICENSE").ShouldBeFalse();
        workspace.FileExists("README.md").ShouldBeFalse();
    }

    [Fact]
    public async Task New_preserves_an_existing_policy_file_when_its_source_is_supplied()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("SECURITY.md", "Existing security policy.\n");
        workspace.WriteFile("replacement.txt", "Replacement policy.\n");
        var source = Path.Combine(workspace.RootPath, "replacement.txt");

        // Act
        var (exitCode, _, error) = await Run(workspace, "--security-source", source, "--force");

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("conflict: SECURITY.md", StringComparison.Ordinal);
        workspace.ReadFile("SECURITY.md").ShouldBe("Existing security policy.\n");
        workspace.FileExists("README.md").ShouldBeFalse();
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

    [Fact]
    public async Task New_with_adds_features_to_the_language_defaults()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var (exitCode, _, error) = await Run(workspace, "--language", "dotnet", "--sdk-version", "10.0.100",
            "--with", "signed-commits", "--with", "readme", "--force");
        var verification = await BaselineVerify(workspace);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        workspace.FileExists("README.md").ShouldBeTrue();
        workspace.FileExists(".gitignore").ShouldBeTrue();
        workspace.FileExists("global.json").ShouldBeTrue();
        await GitConfigShouldBe(workspace, "commit.gpgsign", "true");
        verification.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task New_rejects_combining_with_and_exact_features()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var (exitCode, _, error) = await Run(workspace, "--feature", "readme", "--with", "signed-commits", "--force");

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("--feature and --with", StringComparison.Ordinal);
        workspace.FileExists("README.md").ShouldBeFalse();
    }

    [Theory]
    [InlineData("--language", "cobol", "language")]
    [InlineData("--feature", "magic", "feature")]
    [InlineData("--with", "magic", "feature")]
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
    public async Task New_with_the_dotnet_language_writes_a_nuget_configuration_that_audits_against_nuget_org()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var (exitCode, _, error) = await Run(workspace, "--name", "example-repo", "--language", "dotnet", "--sdk-version", "10.0.100", "--force");

        // Assert
        exitCode.ShouldBe(0, error);
        var configuration = XDocument.Parse(workspace.ReadFile("NuGet.Config")).Root.ShouldNotBeNull();
        var auditSources = configuration.Elements("auditSources").ShouldHaveSingleItem().Elements().ToArray();
        auditSources.Select(static source => source.Name.LocalName).ShouldBe(
            ["clear", "add"],
            "Audit sources keep the audit on nuget.org when a package source is added, and make NuGet report NU1905 when it has no vulnerability data.");
        auditSources[1].Attribute("value").ShouldNotBeNull().Value.ShouldBe("https://api.nuget.org/v3/index.json");
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
    [InlineData("10.0.100")]
    [InlineData("9.0.300")]
    public async Task New_seeds_a_nuget_audit_of_every_dependency_at_every_severity(string sdkVersion)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var (exitCode, _, _) = await Run(workspace, "--name", "example-repo", "--language", "dotnet", "--sdk-version", sdkVersion, "--force");

        // Assert
        exitCode.ShouldBe(0);
        var buildProps = workspace.ReadFile("Directory.Build.props");
        buildProps.ShouldContain("<NuGetAudit>true</NuGetAudit>", StringComparison.Ordinal);
        buildProps.ShouldContain("<NuGetAuditMode>all</NuGetAuditMode>", StringComparison.Ordinal, "Transitive packages are audited also before net10.0, where the default mode is direct.");
        buildProps.ShouldNotContain("NuGetAuditLevel", StringComparison.Ordinal, "NuGet's default level, low, reports advisories of every severity.");
    }

    [Theory]
    [InlineData(new[] { "--language", "dotnet" }, "needs --sdk-version")]
    [InlineData(new[] { "--language", "dotnet", "--sdk-version", "ten" }, "invalid --sdk-version")]
    [InlineData(new[] { "--language", "dotnet", "--sdk-version", "5.0.100" }, "invalid --sdk-version")]
    [InlineData(new[] { "--feature", "global-json", "--sdk-version", "10.0.100" }, "needs --language dotnet")]
    [InlineData(new[] { "--with", "global-json", "--sdk-version", "10.0.100" }, "needs --language dotnet")]
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
