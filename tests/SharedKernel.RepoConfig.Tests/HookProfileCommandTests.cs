namespace SharedKernel.RepoConfig.Tests;

/// <summary>Tests optional Git hooks from the scaffold with local Git operations.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class HookProfileCommandTests
{
    [Fact]
    public async Task New_activates_the_selected_hook_profiles_in_the_baseline()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var creation = await NewTestOperations.Run(workspace, "--feature", "hooks", "--feature", "pre-push-hook", "--feature", "commit-msg-hook", "--force");
        var verification = await NewTestOperations.BaselineVerify(workspace);

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        workspace.FileExists(".githooks/pre-commit").ShouldBeTrue();
        workspace.FileExists(".githooks/pre-push").ShouldBeTrue();
        workspace.FileExists(".githooks/commit-msg").ShouldBeTrue();
        await NewTestOperations.GitConfigShouldBe(workspace, "core.hooksPath", ".githooks");
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(Path.Combine(workspace.RootPath, ".githooks/pre-push")).HasFlag(UnixFileMode.UserExecute).ShouldBeTrue();
            File.GetUnixFileMode(Path.Combine(workspace.RootPath, ".githooks/commit-msg")).HasFlag(UnixFileMode.UserExecute).ShouldBeTrue();
        }

        verification.ExitCode.ShouldBe(0, verification.Error);
    }

    [Fact]
    public async Task Pre_push_ignores_deleted_refs_and_checks_updated_refs()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var remote = new TemporaryRepoConfigWorkspace();
        string[] identity = ["-c", "user.name=Fixture", "-c", "user.email=fixture@example.test", "-c", "commit.gpgsign=false"];
        var creation = await NewTestOperations.Run(workspace, "--feature", "pre-push-hook", "--force");
        var initialize = await HookProfileTestOperations.RunGit(remote, ["init", "--bare", "--template="]);
        var initialCommit = await HookProfileTestOperations.RunGit(workspace, [.. identity, "commit", "--allow-empty", "-m", "feat: create fixture"]);
        var initialPush = await HookProfileTestOperations.RunGit(workspace, ["push", remote.RootPath, "HEAD:refs/heads/obsolete"]);
        workspace.WriteFile("fixture.txt", "Synthetic whitespace fixture. \n");
        var stage = await HookProfileTestOperations.RunGit(workspace, ["add", "--", "fixture.txt"]);
        var whitespaceCommit = await HookProfileTestOperations.RunGit(workspace, [.. identity, "commit", "-m", "feat: add fixture"]);

        // Act
        var deletion = await HookProfileTestOperations.RunGit(workspace, ["push", remote.RootPath, ":refs/heads/obsolete"]);
        var update = await HookProfileTestOperations.RunGit(workspace, ["push", remote.RootPath, "HEAD:refs/heads/topic"]);

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        initialize.ExitCode.ShouldBe(0, initialize.Error);
        initialCommit.ExitCode.ShouldBe(0, initialCommit.Error);
        initialPush.ExitCode.ShouldBe(0, initialPush.Error);
        stage.ExitCode.ShouldBe(0, stage.Error);
        whitespaceCommit.ExitCode.ShouldBe(0, whitespaceCommit.Error);
        deletion.ExitCode.ShouldBe(0, deletion.Error);
        update.ExitCode.ShouldNotBe(0, "The pre-push hook rejects an updated tip with whitespace errors.");
        update.Output.ShouldContain("trailing whitespace", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Commit_msg_forwards_the_message_path_and_propagates_the_verifier_result()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var tools = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("message with spaces.txt", "Synthetic invalid subject\n");
        tools.WriteFile("dotnet", """
            #!/bin/sh
            if [ "$#" != 7 ] || [ "$1 $2 $3 $4 $5 $6" != 'tool run ci -- verify-commit-message --file' ] ||
                [ "$7" != 'message with spaces.txt' ]; then
                exit 99
            fi
            if grep -q '^feat:' "$7"; then
                exit 0
            fi
            printf '%s\n' 'fixture verifier rejected the message' >&2
            exit 1
            """.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Path.Combine(tools.RootPath, "dotnet"), UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        var creation = await NewTestOperations.Run(workspace, "--feature", "commit-msg-hook", "--force");

        // Act
        var rejected = await HookProfileTestOperations.RunGit(workspace, ["hook", "run", "commit-msg", "--", "message with spaces.txt"], tools.RootPath);
        workspace.WriteFile("message with spaces.txt", "feat: accept synthetic subject\n");
        var accepted = await HookProfileTestOperations.RunGit(workspace, ["hook", "run", "commit-msg", "--", "message with spaces.txt"], tools.RootPath);

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        rejected.ExitCode.ShouldBe(1);
        rejected.Error.ShouldContain("fixture verifier rejected the message", StringComparison.Ordinal);
        accepted.ExitCode.ShouldBe(0, accepted.Error);
    }
}
