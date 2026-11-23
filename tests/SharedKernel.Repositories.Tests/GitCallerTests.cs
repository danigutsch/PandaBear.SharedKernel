using PandaBear.Processes;
using PandaBear.Processes.Tests;

namespace PandaBear.Repositories.Tests;

/// <summary>
/// Tests that every git caller of the library runs git the same way: no prompt and none of the host's tokens or Git
/// variables, bounded standard output and standard error, and a time limit that stops the process.
/// </summary>
[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Collection(GitEnvironmentGroup.Name)]
public sealed class GitCallerTests
{
    private static readonly TimeSpan StopLimit = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("baseline")]
    [InlineData("tracked-files")]
    [InlineData("roadmap")]
    [InlineData("text-encoding")]
    public async Task Prompts_tokens_askpass_programs_and_git_variables_never_reach_git(string caller)
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var git = new RecordingFakeExecutable(directory.Path, "git", 1, string.Empty);
        using var environment = new HostileGitEnvironment();

        // Act
        _ = await GitCallers.Call(caller, GitCallers.FakeGit(directory.Path, TimeSpan.FromSeconds(10)), directory.Path);

        // Assert
        git.Started.ShouldBeTrue();
        HostileGitEnvironment.ShouldNotReach(git.Environment());
    }

    [Theory]
    [InlineData("baseline", "exec yes", "InvalidOperationException: git rev-parse failed: The process wrote more output than the limit allows.")]
    [InlineData("baseline", "exec yes >&2", "InvalidOperationException: git rev-parse failed: The process wrote more output than the limit allows.")]
    [InlineData("tracked-files", "exec yes", "InvalidOperationException: Git index inspection failed.")]
    [InlineData("tracked-files", "exec yes >&2", "InvalidOperationException: Git index inspection failed.")]
    [InlineData("roadmap", "exec yes", "InvalidOperationException: Roadmap automation Git inspection failed.")]
    [InlineData("roadmap", "exec yes >&2", "InvalidOperationException: Roadmap automation Git inspection failed.")]
    [InlineData("text-encoding", "exec yes", "issue: Git attribute isolation failed.")]
    [InlineData("text-encoding", "exec yes >&2", "issue: Git attribute isolation failed.")]
    public async Task Endless_output_on_either_stream_fails_and_stops_git(string caller, string body, string expected)
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, body);

        // Act
        var outcome = await GitCallers.Call(caller, GitCallers.FakeGit(directory.Path, TimeSpan.FromSeconds(10)), directory.Path);

        // Assert
        outcome.ShouldBe(expected);
        var stopped = await LinuxProcessState.WaitUntilStopped(git.ProcessId(), StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

    [Theory]
    [InlineData("baseline", "TimeoutException: git did not finish within 0.5 seconds.")]
    [InlineData("tracked-files", "TimeoutException: Git index inspection timed out after 0.5 seconds.")]
    [InlineData("roadmap", "TimeoutException: Roadmap automation Git inspection timed out after 0.5 seconds.")]
    [InlineData("text-encoding", "issue: Git attribute isolation failed.")]
    public async Task A_git_that_hangs_is_stopped_at_the_time_limit(string caller, string expected)
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, "exec sleep 600");

        // Act
        var outcome = await GitCallers.Call(caller, GitCallers.FakeGit(directory.Path, TimeSpan.FromMilliseconds(500)), directory.Path);

        // Assert
        outcome.ShouldBe(expected);
        var stopped = await LinuxProcessState.WaitUntilStopped(git.ProcessId(), StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

    [Fact]
    public async Task A_blob_reader_that_hangs_is_stopped_at_the_time_limit()
    {
        // Arrange
        var realGit = PathExecutableResolver.Default.Find("git");
        if (!OperatingSystem.IsLinux() || realGit is null)
        {
            return;
        }

        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        await repository.Commit("valid.txt", "valid\n", "base", TestContext.Current.CancellationToken);
        using var shim = new TemporaryDirectory();
        var processIdPath = Path.Combine(shim.Path, "git.pid");
        FakeExecutable.WriteScript(
            Path.Combine(shim.Path, "git"),
            $"""
            case " $* " in
              *" cat-file --batch "*) printf '%s' "$$" > {FakeExecutable.Quote(processIdPath)}; exec sleep 600 ;;
            esac
            exec {FakeExecutable.Quote(realGit)} "$@"
            """);

        // Act
        var issues = await TextEncodingVerifier.Verify(GitCallers.FakeGit(shim.Path, TimeSpan.FromSeconds(2)), repository.RootPath, TestContext.Current.CancellationToken);

        // Assert
        issues.ShouldBe([new VerificationIssue(".", "Git blob inspection failed.")]);
        var processId = ProcessIdFile.Read(processIdPath);
        var stopped = await LinuxProcessState.WaitUntilStopped(processId, StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

}
