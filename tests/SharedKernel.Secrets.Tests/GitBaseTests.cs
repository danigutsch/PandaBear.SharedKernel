using PandaBear.Processes.Tests;

namespace PandaBear.Secrets.Tests;

/// <summary>
/// Tests that the base-revision reader runs git as every other git caller does: no prompt and none of the host's tokens or
/// Git variables, bounded output, and a time limit that stops the process.
/// </summary>
[Trait("Capability", "Secrets")]
[Collection(GitEnvironmentGroup.Name)]
public sealed class GitBaseTests
{
    private const string SkipReason = "The Unix tools are not available.";
    private static readonly TimeSpan StopLimit = TimeSpan.FromSeconds(10);

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = SkipReason)]
    public async Task Prompts_tokens_askpass_programs_and_git_variables_never_reach_git()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, string.Empty);
        using var environment = new HostileGitEnvironment();

        // Act
        _ = await GitBase.Open(FakeGitPrograms.Create(git.Path), directory.Path, "main", TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        HostileGitEnvironment.ShouldNotReach(git.Environment());
    }

    [Theory(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = SkipReason)]
    [InlineData("exec " + UnixToolsProbe.Yes)]
    [InlineData("exec " + UnixToolsProbe.Yes + " >&2")]
    public async Task Endless_output_on_either_stream_fails_and_stops_git(string body)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, body);

        // Act
        Func<Task> open = () => GitBase.Open(FakeGitPrograms.Create(git.Path), directory.Path, "main", TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        var exception = await open.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe("git could not run: The process wrote more output than the limit allows.");
        var stopped = await LinuxProcessState.WaitUntilStopped(git.ProcessId(), StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = SkipReason)]
    public async Task A_git_that_hangs_is_stopped_at_the_time_limit()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, "exec /bin/sleep 600");

        // Act
        Func<Task> open = () => GitBase.Open(FakeGitPrograms.Create(git.Path), directory.Path, "main", TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        // Assert
        var exception = await open.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe("git could not run: git did not finish within 0.5 seconds.");
        var stopped = await LinuxProcessState.WaitUntilStopped(git.ProcessId(), StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

}
