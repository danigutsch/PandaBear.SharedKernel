using System.ComponentModel;

namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// Tests how the text-encoding verifier handles misbehaving, hanging, and spoofed Git processes.
/// </summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class TextEncodingProcessTests
{
    /// <summary>
    /// Verifies that oversized git check-attr output makes verify-text-encoding exit 1 with a Git attribute inspection error within the watchdog timeout.
    /// </summary>
    [Fact]
    public async Task Oversized_attribute_output_fails_without_hanging()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteText("valid.txt", "valid\n");
        await repository.Stage("valid.txt", TestContext.Current.CancellationToken);
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Dictionary<string, string?> environment = new(StringComparer.Ordinal)
        {
            ["PATH"] = repository.CreateGitShim(),
            ["TEXT_ENCODING_GIT_SHIM_MODE"] = "oversized-attributes"
        };

        // Act
        var result = await repository.RunCommand(
            ["verify-text-encoding", "--root", repository.RootPath],
            watchdog.Token,
            environment: environment);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("Git attribute inspection failed.", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a malformed or oversized first blob response makes verify-text-encoding exit 1 with the expected message without hanging.
    /// </summary>
    [Theory]
    [InlineData("malformed-blob", "Git blob inspection failed.")]
    [InlineData("oversized-blob", "Blob exceeds the 64 MiB text scan limit.")]
    public async Task First_blob_protocol_failures_stop_before_later_requests_without_hanging(string mode, string expectedMessage)
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteText("first.txt", "valid\n");
        repository.WriteText("later.txt", "valid\n");
        await repository.Stage("first.txt", TestContext.Current.CancellationToken);
        await repository.Stage("later.txt", TestContext.Current.CancellationToken);
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Dictionary<string, string?> environment = new(StringComparer.Ordinal)
        {
            ["PATH"] = repository.CreateGitShim(),
            ["TEXT_ENCODING_GIT_SHIM_MODE"] = mode
        };

        // Act
        var result = await repository.RunCommand(
            ["verify-text-encoding", "--root", repository.RootPath],
            watchdog.Token,
            environment: environment);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain(expectedMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that cancelling the token stops a Git blob process that never answers and yields exit code 1 with no output.
    /// </summary>
    [Fact]
    public async Task Cancellation_stops_a_hanging_blob_process()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteText("valid.txt", "valid\n");
        await repository.Stage("valid.txt", TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        Dictionary<string, string?> environment = new(StringComparer.Ordinal)
        {
            ["PATH"] = repository.CreateGitShim(),
            ["TEXT_ENCODING_GIT_SHIM_MODE"] = "hanging-blob"
        };

        // Act
        var result = await repository.RunCommand(
            ["verify-text-encoding", "--root", repository.RootPath],
            cancellation.Token,
            environment: environment);

        // Assert
        result.ExitCode.ShouldBe(1);
        cancellation.IsCancellationRequested.ShouldBeTrue();
        result.StandardOutput.ShouldBe(string.Empty);
        result.StandardError.ShouldBe(string.Empty);
    }

    /// <summary>
    /// Verifies that a Git blob process that keeps running after its input closes is stopped and reported as a Git blob inspection failure.
    /// </summary>
    [Fact]
    public async Task Blob_process_that_ignores_closed_input_is_bounded_and_stopped()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteText("valid.txt", "valid\n");
        await repository.Stage("valid.txt", TestContext.Current.CancellationToken);
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Dictionary<string, string?> environment = new(StringComparer.Ordinal)
        {
            ["PATH"] = repository.CreateGitShim(),
            ["TEXT_ENCODING_GIT_SHIM_MODE"] = "lingering-blob"
        };

        // Act
        var result = await repository.RunCommand(
            ["verify-text-encoding", "--root", repository.RootPath],
            watchdog.Token,
            environment: environment);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("Git blob inspection failed.", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that ObserveCleanup returns a completed task when the observed cleanup task never completes within the timeout.
    /// </summary>
    [Fact]
    public async Task Cleanup_observation_is_bounded_when_the_task_never_completes()
    {
        // Arrange
        TaskCompletionSource pending = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var observation = TextEncodingVerifier.ObserveCleanup(pending.Task, TimeSpan.Zero);

        // Assert
        observation.IsCompleted.ShouldBeTrue();
        await observation;
        pending.Task.IsCompleted.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies that ObserveCleanup swallows Win32Exception and UnauthorizedAccessException faults from process cleanup.
    /// </summary>
    [Theory]
    [InlineData("win32")]
    [InlineData("unauthorized")]
    public async Task Cleanup_observation_swallows_process_cleanup_faults(string faultKind)
    {
        // Arrange
        Exception exception = faultKind == "win32"
            ? new Win32Exception("cleanup failed")
            : new UnauthorizedAccessException("cleanup failed");
        var cleanup = Task.FromException(exception);

        // Act
        await TextEncodingVerifier.ObserveCleanup(cleanup, TimeSpan.Zero);

        // Assert
        cleanup.IsFaulted.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies that a git executable found through a relative PATH entry in the repository is never run and the staged invalid UTF-8 file is still reported.
    /// </summary>
    [Fact]
    public async Task Verify_text_encoding_ignores_repository_local_git_from_relative_path_entries()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteBytes("invalid.txt", [0xFF]);
        await repository.Stage("invalid.txt", TestContext.Current.CancellationToken);
        var fakeGit = await RepositoryLocalGit.Create(repository.RootPath, TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(
            ["verify-text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken,
            environment: fakeGit.EnvironmentVariables);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("invalid.txt: Is not valid UTF-8.", StringComparison.Ordinal);
        fakeGit.Ran.ShouldBeFalse("Git resolves only from absolute PATH entries, so the repository-local fake never runs.");
    }
}
