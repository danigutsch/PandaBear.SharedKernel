using SharedKernel.Processes.Tests;

namespace SharedKernel.Testing.Tests;

/// <summary>
/// Tests that the <c>git</c> that test setup runs gets no prompt and none of the host's tokens or Git variables, keeps
/// the isolating variables of <see cref="TestGit.Environment"/>, and has bounded output and a time limit. The fake git is
/// first on <c>PATH</c>, so the tests share the serial collection that changes the process environment.
/// </summary>
[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Collection(GitEnvironmentGroup.Name)]
public sealed class TestGitTests
{
    private static readonly TimeSpan StopLimit = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Prompts_tokens_askpass_programs_and_inherited_git_variables_never_reach_git()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, string.Empty);
        using var path = FakeGitPath.Prepend(directory.Path);
        using var environment = new HostileGitEnvironment();

        // Act
        var result = await TestGit.TryRun(directory.Path, ["status"], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        var recorded = git.Environment();
        HostileGitEnvironment.ShouldNotReach(recorded);
        recorded.ShouldContain("GIT_CONFIG_NOSYSTEM=1");
    }

    [Theory]
    [InlineData("head -c 20000000 /dev/zero")]
    [InlineData("head -c 20000000 /dev/zero >&2")]
    public async Task Output_above_the_limit_on_either_stream_fails_and_stops_git(string body)
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, body);
        using var path = FakeGitPath.Prepend(directory.Path);
        Func<Task> run = () => TestGit.TryRun(directory.Path, ["status"], TestContext.Current.CancellationToken);

        // Act
        var exception = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("The process wrote more output than the limit allows.");
        var stopped = await LinuxProcessState.WaitUntilStopped(git.ProcessId(), StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

    [Fact]
    public async Task A_git_that_hangs_is_stopped_at_the_thirty_second_time_limit()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, "exec sleep 600");
        using var path = FakeGitPath.Prepend(directory.Path);
        Func<Task> run = () => TestGit.TryRun(directory.Path, ["status"], TestContext.Current.CancellationToken);

        // Act
        var exception = await run.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldBe("The process did not finish within 30 seconds.");
        var stopped = await LinuxProcessState.WaitUntilStopped(git.ProcessId(), StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

}
