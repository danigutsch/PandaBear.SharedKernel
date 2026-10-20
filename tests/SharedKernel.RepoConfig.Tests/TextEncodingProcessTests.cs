using System.ComponentModel;

namespace SharedKernel.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection("Repo config tool environment")]
public sealed class TextEncodingProcessTests
{
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
            ["text-encoding", "--root", repository.RootPath],
            watchdog.Token,
            environment: environment);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("Git attribute inspection failed.", StringComparison.Ordinal);
    }

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
            ["text-encoding", "--root", repository.RootPath],
            watchdog.Token,
            environment: environment);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain(expectedMessage, StringComparison.Ordinal);
    }

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
        var action = (Func<Task>)(async () => await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            cancellation.Token,
            environment: environment));
        var exception = await action.ShouldThrow<OperationCanceledException>();

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

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
            ["text-encoding", "--root", repository.RootPath],
            watchdog.Token,
            environment: environment);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("Git blob inspection failed.", StringComparison.Ordinal);
    }

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

    [Fact]
    public async Task Text_encoding_ignores_repository_local_git_from_relative_path_entries()
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
        var markerPath = Path.Combine(repository.RootPath, "fake-git-ran");
        var fakeGitPath = Path.Combine(repository.RootPath, "git");
        await File.WriteAllTextAsync(
            fakeGitPath,
            "#!/bin/sh\n: > \"$TEXT_ENCODING_FAKE_GIT_MARKER\"\nexit 99\n",
            TestContext.Current.CancellationToken);
        File.SetUnixFileMode(
            fakeGitPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var relativeRoot = Path.GetRelativePath(Environment.CurrentDirectory, repository.RootPath);
        Dictionary<string, string?> environment = new(StringComparer.Ordinal)
        {
            ["PATH"] = $"{relativeRoot}{Path.PathSeparator}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}",
            ["TEXT_ENCODING_FAKE_GIT_MARKER"] = markerPath
        };

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken,
            environment: environment);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("invalid.txt: Is not valid UTF-8.", StringComparison.Ordinal);
        File.Exists(markerPath).ShouldBeFalse();
    }
}
