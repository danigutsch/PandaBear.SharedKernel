using SharedKernel.Processes.Tests;

namespace SharedKernel.DevContainers.Tests;

/// <summary>
/// Tests that the <c>git</c> that finds the common directory of a linked worktree gets no prompt and none of the host's
/// tokens or Git variables, bounded output, and a time limit that stops it.
/// </summary>
[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Collection(GitEnvironmentGroup.Name)]
public sealed class DevcontainerGitTests
{
    private static readonly TimeSpan StopLimit = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Prompts_tokens_askpass_programs_and_git_variables_never_reach_git()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = LinkedWorktreeGit.CreateWorktree();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, "printf '%s\\n' /repository/.git");
        using var environment = new HostileGitEnvironment();

        // Act
        var commonDirectory = await LinkedWorktreeGit.Find(directory.Path, TimeSpan.FromSeconds(10));

        // Assert
        commonDirectory.ShouldBe("/repository/.git");
        HostileGitEnvironment.ShouldNotReach(git.Environment());
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

        using var directory = LinkedWorktreeGit.CreateWorktree();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, body);
        Func<Task> find = () => LinkedWorktreeGit.Find(directory.Path, TimeSpan.FromSeconds(10));

        // Act
        var exception = await find.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("The process wrote more output than the limit allows.");
        var stopped = await LinuxProcessState.WaitUntilStopped(git.ProcessId(), StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

    [Fact]
    public async Task A_git_that_hangs_is_stopped_at_the_time_limit()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = LinkedWorktreeGit.CreateWorktree();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, "exec sleep 600");
        Func<Task> find = () => LinkedWorktreeGit.Find(directory.Path, TimeSpan.FromMilliseconds(500));

        // Act
        var exception = await find.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldBe("The process did not finish within 0.5 seconds.");
        var stopped = await LinuxProcessState.WaitUntilStopped(git.ProcessId(), StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

}
