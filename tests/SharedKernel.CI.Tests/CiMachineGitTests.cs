using PandaBear.Processes.Tests;

namespace PandaBear.CI.Tests;

/// <summary>
/// Tests that every <c>git</c> run of the CI engine, which its selection, coverage, local-check, slice, and candidate
/// steps all make through the machine, gets no prompt and none of the host's tokens or Git variables, bounded output,
/// and a time limit that stops the process.
/// </summary>
[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Collection(GitEnvironmentGroup.Name)]
public sealed class CiMachineGitTests
{
    private const string Flood = "head -c 20000000 /dev/zero";
    private static readonly TimeSpan StopLimit = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("run")]
    [InlineData("run-git")]
    public async Task Prompts_tokens_askpass_programs_and_git_variables_never_reach_git(string entry)
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
        _ = await FakeGitMachine.Run(FakeGitMachine.Create(directory.Path, TimeSpan.FromSeconds(10)), entry, directory.Path);

        // Assert
        HostileGitEnvironment.ShouldNotReach(git.Environment());
    }

    [Theory]
    [InlineData("run", Flood)]
    [InlineData("run", Flood + " >&2")]
    [InlineData("run-git", Flood)]
    [InlineData("run-git", Flood + " >&2")]
    public async Task Output_above_the_limit_on_either_stream_fails_and_stops_git(string entry, string body)
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, body);
        Func<Task> run = () => FakeGitMachine.Run(FakeGitMachine.Create(directory.Path, TimeSpan.FromSeconds(10)), entry, directory.Path);

        // Act
        var exception = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("The process wrote more output than the limit allows.");
        var stopped = await LinuxProcessState.WaitUntilStopped(git.ProcessId(), StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

    [Theory]
    [InlineData("run")]
    [InlineData("run-git")]
    public async Task A_git_that_hangs_is_stopped_at_the_time_limit(string entry)
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, "exec sleep 600");
        Func<Task> run = () => FakeGitMachine.Run(FakeGitMachine.Create(directory.Path, TimeSpan.FromMilliseconds(500)), entry, directory.Path);

        // Act
        var exception = await run.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldBe("git status did not exit within 0.5 seconds.");
        var stopped = await LinuxProcessState.WaitUntilStopped(git.ProcessId(), StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

}
