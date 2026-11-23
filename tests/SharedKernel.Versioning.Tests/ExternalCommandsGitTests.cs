using PandaBear.Processes.Tests;

namespace PandaBear.Versioning.Tests;

/// <summary>
/// Tests that every <c>git</c> run of the versioning engine (release history, candidate validation, and consumer
/// verification) gets no prompt and none of the host's tokens or Git variables, bounded output, and a time limit.
/// </summary>
[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Collection(GitEnvironmentGroup.Name)]
public sealed class ExternalCommandsGitTests
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

        using var directory = new TemporaryDirectory();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, string.Empty);
        using var environment = new HostileGitEnvironment();

        // Act
        _ = await FakeGitCommands.Create(directory.Path, TimeSpan.FromSeconds(10)).Run("git", ["status"], directory.Path, TestContext.Current.CancellationToken);

        // Assert
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

        using var directory = new TemporaryDirectory();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, body);
        Func<Task> run = () => FakeGitCommands.Create(directory.Path, TimeSpan.FromSeconds(10)).Run("git", ["status"], directory.Path, TestContext.Current.CancellationToken);

        // Act
        var exception = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("The process wrote more output than the limit allows.");
        var stopped = await LinuxProcessState.WaitUntilStopped(git.ProcessId(), StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

    [Fact]
    public async Task A_git_that_hangs_is_stopped_at_the_time_limit_and_reported_as_a_failed_command()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, "exec sleep 600");
        Func<Task> run = () => FakeGitCommands.Create(directory.Path, TimeSpan.FromMilliseconds(500)).Run("git", ["status"], directory.Path, TestContext.Current.CancellationToken);

        // Act
        var exception = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("git status did not exit within 0.5 seconds.");
        var stopped = await LinuxProcessState.WaitUntilStopped(git.ProcessId(), StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

}
