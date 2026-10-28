using System.Runtime.Versioning;
using static PandaBear.RepoConfig.Tests.BaselineGitTestOperations;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the <c>gitConfig</c> and <c>hooks</c> parts of the <c>baseline</c> commands against real Git repositories.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class BaselineGitTests
{
    private const string Settings = """
        {
          "version": 1,
          "baseline": {
            "repositoryName": "example-repo",
            "gitConfig": [
              { "key": "init.defaultBranch", "value": "trunk" },
              { "key": "pull.ff", "value": "only" }
            ],
            "hooks": { "hooks": [ { "name": "pre-commit", "profile": "pre-commit-diff-check" } ] }
          }
        }
        """;

    [Theory]
    [InlineData("core.fsmonitor")]
    [InlineData("core.sshCommand")]
    [InlineData("core.pager")]
    [InlineData("core.editor")]
    [InlineData("core.hooksPath")]
    [InlineData("credential.helper")]
    [InlineData("alias.co")]
    [InlineData("include.path")]
    [InlineData("includeIf.gitdir:/x/.path")]
    [InlineData("url.https://example.invalid/.insteadOf")]
    [InlineData("diff.tool")]
    [InlineData("merge.tool")]
    [InlineData("filter.x.clean")]
    [InlineData("unknown.key")]
    [InlineData("--global")]
    public async Task Git_config_rejects_every_key_outside_the_allowlist(string key)
    {
        // Arrange
        using var workspace = BaselineTestOperations.CreateWorkspace(
            $$"""{ "version": 1, "baseline": { "repositoryName": "r", "gitConfig": [ { "key": "{{key}}", "value": "x" } ] } }""");

        // Act
        var (exitCode, _, error) = await BaselineTestOperations.Run(workspace, "verify");

        // Assert
        exitCode.ShouldBe(1, "A key outside the allowlist is a configuration error.");
        error.ShouldContain("$.baseline.gitConfig[0].key", StringComparison.Ordinal, "The error names the JSON path.");
    }

    [Theory]
    [InlineData("a\\nb")]
    [InlineData("-x")]
    [InlineData("")]
    public async Task Git_config_rejects_unsafe_values(string value)
    {
        // Arrange
        using var workspace = BaselineTestOperations.CreateWorkspace(
            $$"""{ "version": 1, "baseline": { "repositoryName": "r", "gitConfig": [ { "key": "pull.ff", "value": "{{value}}" } ] } }""");

        // Act
        var (exitCode, _, error) = await BaselineTestOperations.Run(workspace, "verify");

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("$.baseline.gitConfig[0].value", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "hooks": [ { "name": "pre-commit", "profile": "pre-commit-diff-check" } ], "directory": ".git/hooks" }""", "$.baseline.hooks.directory")]
    [InlineData("""{ "hooks": [ { "name": "pre-commit", "profile": "pre-commit-diff-check" } ], "directory": "sub/.GIT." }""", "$.baseline.hooks.directory")]
    [InlineData("""{ "hooks": [ { "name": "../x", "profile": "pre-commit-diff-check" } ] }""", "$.baseline.hooks.hooks[0].name")]
    [InlineData("""{ "hooks": [ { "name": "pre-commit" } ] }""", "$.baseline.hooks.hooks[0] must name exactly one of profile and source")]
    [InlineData("""{ "hooks": [ { "name": "pre-commit", "source": "/x" } ] }""", "$.baseline.hooks.hooks[0].source")]
    public async Task Hooks_reject_unsafe_directories_names_and_sources(string hooks, string expectedFragment)
    {
        // Arrange
        using var workspace = BaselineTestOperations.CreateWorkspace(
            $$"""{ "version": 1, "baseline": { "repositoryName": "r", "hooks": {{hooks}} } }""");

        // Act
        var (exitCode, _, error) = await BaselineTestOperations.Run(workspace, "verify");

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain(expectedFragment, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_previews_settings_and_hooks_without_touching_git_or_the_tree()
    {
        // Arrange
        using var repository = await CreateRepository(Settings);

        // Act
        var result = await Baseline(repository, "apply");

        // Assert
        result.ExitCode.ShouldBe(0, result.StandardError);
        result.StandardOutput.ShouldContain("preview: would create .githooks/pre-commit.", StringComparison.Ordinal);
        result.StandardOutput.ShouldContain("preview: would set git config init.defaultBranch.", StringComparison.Ordinal);
        result.StandardOutput.ShouldContain("preview: would set git config core.hooksPath.", StringComparison.Ordinal);
        File.Exists(Path.Combine(repository.RootPath, ".githooks", "pre-commit")).ShouldBeFalse("A preview writes no hook.");
        var value1 = await ConfigValue(repository, "init.defaultBranch");
        value1.ShouldBe(string.Empty, "A preview sets nothing.");
    }

    [Fact]
    public async Task Apply_with_force_installs_a_tracked_hook_sets_the_allowed_settings_and_is_idempotent()
    {
        // Arrange
        using var repository = await CreateRepository(Settings);

        // Act
        var first = await Baseline(repository, "apply", force: true);
        var verification = await Baseline(repository, "verify");
        var second = await Baseline(repository, "apply", force: true);

        // Assert
        first.ExitCode.ShouldBe(0, first.StandardError);
        var hook = await File.ReadAllTextAsync(Path.Combine(repository.RootPath, ".githooks", "pre-commit"), TestContext.Current.CancellationToken);
        hook.ShouldStartWith("#!/bin/sh\n# Managed by sharedkernel-repo baseline (sha256 ", StringComparison.Ordinal, "The marker follows the shebang.");
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(Path.Combine(repository.RootPath, ".githooks", "pre-commit"))
                .HasFlag(UnixFileMode.UserExecute).ShouldBeTrue("The hook is executable.");
        }

        var value2 = await ConfigValue(repository, "init.defaultBranch");
        value2.ShouldBe("trunk");
        var value3 = await ConfigValue(repository, "pull.ff");
        value3.ShouldBe("only");
        var value4 = await ConfigValue(repository, "core.hooksPath");
        value4.ShouldBe(".githooks", "The tracked directory is the only hooks path.");
        Directory.GetFiles(Path.Combine(repository.RootPath, ".git", "hooks"), "pre-commit").ShouldBeEmpty("Nothing is written into .git/hooks.");
        verification.ExitCode.ShouldBe(0, verification.StandardError);
        second.ExitCode.ShouldBe(0);
        second.StandardOutput.ShouldContain("0 created, 0 updated, 0 overwritten, and 4 unchanged files and settings.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_setting_and_hook_drift()
    {
        // Arrange
        using var repository = await CreateRepository(Settings);
        _ = await Baseline(repository, "apply", force: true);
        _ = await repository.RunGit(["config", "--local", "pull.ff", "false"], TestContext.Current.CancellationToken);
        _ = await repository.RunGit(["config", "--local", "--unset", "init.defaultBranch"], TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(repository.RootPath, ".githooks", "pre-commit"), "#!/bin/sh\nexit 0\n", TestContext.Current.CancellationToken);

        // Act
        var result = await Baseline(repository, "verify");

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("- .githooks/pre-commit: exists without the baseline marker.", StringComparison.Ordinal);
        result.StandardError.ShouldContain("- git config init.defaultBranch: not set.", StringComparison.Ordinal);
        result.StandardError.ShouldContain("- git config pull.ff: set to a different value.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_treats_a_different_existing_setting_as_a_conflict_unless_the_entry_opts_in()
    {
        // Arrange
        using var repository = await CreateRepository(Settings);
        _ = await repository.RunGit(["config", "--local", "pull.ff", "false"], TestContext.Current.CancellationToken);

        // Act
        var conflict = await Baseline(repository, "apply", force: true);
        var hookExistsAfterConflict = File.Exists(Path.Combine(repository.RootPath, ".githooks", "pre-commit"));
        repository.WriteText(".config/sharedkernel-repo.json", Settings.Replace(
            """{ "key": "pull.ff", "value": "only" }""", """{ "key": "pull.ff", "value": "only", "overwriteExisting": true }""", StringComparison.Ordinal));
        var overwrite = await Baseline(repository, "apply", force: true);

        // Assert
        conflict.ExitCode.ShouldBe(1);
        conflict.StandardError.ShouldBe("conflict: git config pull.ff is set to a different value; set overwriteExisting on its entry to replace it." + Environment.NewLine);
        hookExistsAfterConflict.ShouldBeFalse("A conflict writes nothing.");
        overwrite.ExitCode.ShouldBe(0, overwrite.StandardError);
        var value5 = await ConfigValue(repository, "pull.ff");
        value5.ShouldBe("only");
    }

    [Fact(SkipUnless = nameof(PosixTestSupport.CanRestrictPermissions), SkipType = typeof(PosixTestSupport), Skip = "Needs POSIX permissions that bind the current user.")]
    [UnsupportedOSPlatform("windows")]
    public async Task Verify_and_apply_report_a_hook_that_lost_its_executable_bit()
    {
        // Arrange
        using var repository = await CreateRepository(Settings);
        _ = await Baseline(repository, "apply", force: true);
        var hook = Path.Combine(repository.RootPath, ".githooks", "pre-commit");
        File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        // Act
        var drift = await Baseline(repository, "verify");
        var fix = await Baseline(repository, "apply", force: true);

        // Assert
        drift.ExitCode.ShouldBe(1);
        drift.StandardError.ShouldContain("- .githooks/pre-commit: is not executable.", StringComparison.Ordinal);
        fix.ExitCode.ShouldBe(0, fix.StandardError);
        File.GetUnixFileMode(hook).HasFlag(UnixFileMode.UserExecute).ShouldBeTrue("Apply restores the executable bit.");
    }

    [Fact]
    public async Task Settings_fail_when_the_root_is_not_the_top_of_a_git_repository()
    {
        // Arrange
        using var repository = await CreateRepository(Settings);
        var nested = Path.Combine(repository.RootPath, "nested");
        Directory.CreateDirectory(Path.Combine(nested, ".config"));
        await File.WriteAllTextAsync(Path.Combine(nested, ".config", "sharedkernel-repo.json"), Settings, TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(["baseline", "apply", "--force", "--root", nested], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("not the top level of a Git repository", StringComparison.Ordinal);
        File.Exists(Path.Combine(nested, ".githooks", "pre-commit")).ShouldBeFalse("Nothing is written.");
        var value6 = await ConfigValue(repository, "core.hooksPath");
        value6.ShouldBe(string.Empty, "The enclosing repository is untouched.");
    }
}
