namespace SharedKernel.RepoConfig.Tests;

/// <summary>Exercises the optional tracked-file verification profile through the repo command.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class TrackedCheckCommandTests
{
    [Fact]
    public async Task The_optional_profile_seeds_a_configuration_that_requires_real_lanes()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var creation = await NewTestOperations.Run(workspace, "--with", "check-runner", "--force");

        // Act
        var result = await NewTestOperations.CheckTracked(workspace);

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        workspace.FileExists(TrackedCheckRunner.ConfigurationPath).ShouldBeTrue();
        result.ExitCode.ShouldBe(1, result.Error);
        result.Output.ShouldContain("FAIL: no checks ran", StringComparison.Ordinal);
        result.Output.ShouldContain("ran 0, skipped 0", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, false, 0, "skip:", "ran 1, skipped 1")]
    [InlineData(false, true, 1, "FAIL:", "ran 1, skipped 0")]
    [InlineData(true, false, 0, "skip:", "ran 1, skipped 1")]
    [InlineData(true, true, 1, "FAIL:", "ran 1, skipped 0")]
    public async Task The_runner_sends_only_nul_delimited_tracked_paths_and_reports_tool_absence(
        bool reportedByChild, bool strict, int expectedExitCode, string expectedStatus, string expectedSummary)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var creation = await NewTestOperations.Run(workspace, "--with", "check-runner", "--force");
        workspace.WriteFile("known input.txt", "tracked");
        workspace.WriteFile("untracked.txt", "not staged");
        workspace.WriteFile("expected-paths.bin", "known input.txt\0");
        var stage = await HookProfileTestOperations.RunGit(workspace, ["add", "--", "known input.txt"]);
        var digest = await HookProfileTestOperations.RunGit(workspace, ["hash-object", "expected-paths.bin"]);
        var unavailable = reportedByChild ? "git" : "missing-scaffold-check-fixture-tool";
        var command = reportedByChild ? """["git", "-c", "alias.fixture-skip=!exit 77", "fixture-skip"]""" : """["missing-scaffold-check-fixture-tool"]""";
        workspace.WriteFile(TrackedCheckRunner.ConfigurationPath, $$"""
            { "lanes": [
                { "pathspec": "*.txt", "command": ["git", "hash-object", "--stdin"] },
                { "pathspec": "*.txt", "command": {{command}} }
            ] }
            """);

        // Act
        var result = await NewTestOperations.CheckTracked(workspace, strict ? ["--strict"] : []);

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        stage.ExitCode.ShouldBe(0, stage.Error);
        digest.ExitCode.ShouldBe(0, digest.Error);
        result.ExitCode.ShouldBe(expectedExitCode, result.Error);
        result.Output.ShouldContain(digest.Output.Trim(), StringComparison.Ordinal);
        result.Output.ShouldContain("ok: git", StringComparison.Ordinal);
        result.Output.ShouldContain($"{expectedStatus} {unavailable} (tool unavailable)", StringComparison.Ordinal);
        result.Output.ShouldContain(expectedSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Empty_scan_scope_fails_without_executing_the_lane()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var creation = await NewTestOperations.Run(workspace, "--with", "check-runner", "--force");
        workspace.WriteFile("fixture.txt", "text");
        var stage = await HookProfileTestOperations.RunGit(workspace, ["add", "--", "fixture.txt"]);
        workspace.WriteFile(TrackedCheckRunner.ConfigurationPath, """
            { "lanes": [{ "pathspec": "*.missing", "command": ["git", "-c", "alias.fixture=!echo should-not-run", "fixture"] }] }
            """);

        // Act
        var result = await NewTestOperations.CheckTracked(workspace);

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        stage.ExitCode.ShouldBe(0, stage.Error);
        result.ExitCode.ShouldBe(1, result.Error);
        result.Output.ShouldContain("FAIL: git (no tracked files)", StringComparison.Ordinal);
        result.Output.ShouldNotContain("should-not-run", StringComparison.Ordinal);
        result.Output.ShouldContain("ran 0, skipped 0", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tracked_file_missing_from_the_checkout_cannot_make_the_lane_pass()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var creation = await NewTestOperations.Run(workspace, "--with", "check-runner", "--force");
        workspace.WriteFile("fixture.txt", "tracked");
        var stage = await HookProfileTestOperations.RunGit(workspace, ["add", "--", "fixture.txt"]);
        workspace.DeleteFile("fixture.txt");
        workspace.WriteFile(TrackedCheckRunner.ConfigurationPath, """
            { "lanes": [{ "pathspec": "*.txt", "command": ["git", "hash-object", "--stdin"] }] }
            """);

        // Act
        var result = await NewTestOperations.CheckTracked(workspace);

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        stage.ExitCode.ShouldBe(0, stage.Error);
        result.ExitCode.ShouldBe(1, result.Error);
        result.Output.ShouldContain("no tracked files", StringComparison.Ordinal);
        result.Output.ShouldContain("ran 0, skipped 0", StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_runner_checks_the_actual_exit_status_and_reports_each_lane()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var creation = await NewTestOperations.Run(workspace, "--with", "check-runner", "--force");
        workspace.WriteFile("fixture.txt", "text");
        var stage = await HookProfileTestOperations.RunGit(workspace, ["add", "--", "fixture.txt"]);
        workspace.WriteFile("custom.json", """
            { "lanes": [
                { "pathspec": "*.txt", "command": ["git", "-c", "alias.fixture-fail=!echo renderer-failed >&2; exit 9", "fixture-fail"] },
                { "pathspec": "*.txt", "command": ["git", "hash-object", "--stdin"] }
            ] }
            """);

        // Act
        var result = await NewTestOperations.CheckTracked(workspace, "--config", "custom.json");

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        stage.ExitCode.ShouldBe(0, stage.Error);
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("renderer-failed", StringComparison.Ordinal);
        result.Output.ShouldContainInOrder(["FAIL: git (exit 9)", "ok: git", "ran 2, skipped 0"]);
    }

    [Fact]
    public async Task A_bad_pathspec_is_a_failed_lane_and_the_report_counts_later_checks()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var creation = await NewTestOperations.Run(workspace, "--with", "check-runner", "--force");
        workspace.WriteFile("fixture.txt", "tracked");
        var stage = await HookProfileTestOperations.RunGit(workspace, ["add", "--", "fixture.txt"]);
        workspace.WriteFile(TrackedCheckRunner.ConfigurationPath, """
            { "lanes": [
                { "pathspec": ":(unknown-magic)fixture.txt", "command": ["git", "hash-object", "--stdin"] },
                { "pathspec": "*.txt", "command": ["git", "hash-object", "--stdin"] }
            ] }
            """);

        // Act
        var result = await NewTestOperations.CheckTracked(workspace);

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        stage.ExitCode.ShouldBe(0, stage.Error);
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContainInOrder(["FAIL: git (Git index inspection failed)", "ok: git", "ran 1, skipped 0"]);
    }

    [Fact]
    public async Task The_runner_requires_a_git_work_tree()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var initialize = await HookProfileTestOperations.RunGit(workspace, ["init", "--bare", "--template="]);

        // Act
        var result = await NewTestOperations.CheckTracked(workspace);

        // Assert
        initialize.ExitCode.ShouldBe(0, initialize.Error);
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("FAIL: checks require a Git work tree", StringComparison.Ordinal);
        result.Output.ShouldContain("ran 0, skipped 0", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}", "requires a lanes array")]
    [InlineData("""{"lanes":[{"pathspec":"*.txt","command":[]}]}""", "needs a Git pathspec and a nonempty command array")]
    [InlineData("""{"lanes":[{"pathspec":"*.txt","command":["git",null]}]}""", "arguments must be strings")]
    [InlineData("""{"lanes":[{"pathspec":"*.txt","command":[" "]}]}""", "needs an executable name")]
    public async Task Invalid_lane_configuration_fails_before_running_a_lane(string configuration, string message)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var creation = await NewTestOperations.Run(workspace, "--with", "check-runner", "--force");
        workspace.WriteFile(TrackedCheckRunner.ConfigurationPath, configuration);

        // Act
        var result = await NewTestOperations.CheckTracked(workspace);

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain(message, StringComparison.Ordinal);
        result.Output.ShouldNotContain("ok:", StringComparison.Ordinal);
    }
}
