using System.Net;
using System.Net.Sockets;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the shared process, Git, loopback port and tracking helpers.</summary>
public sealed class ProcessGitAndPortHelperTests
{
    [Fact]
    public static void Loopback_port_returns_a_port_that_can_be_bound()
    {
        // Arrange
        var port = LoopbackPort.GetFree();

        // Act
        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        // Assert
        ((IPEndPoint)listener.LocalEndpoint).Port.ShouldBe(port);
    }

    [Fact]
    public static async Task Test_process_captures_exit_code_and_output()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await TestProcess.Run("git", Environment.CurrentDirectory, ["--version"], ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldStartWith("git version", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Test_git_run_reports_the_standard_error_of_a_failing_command()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        Func<Task> run = async () => await TestGit.Run(directory.Path, ["rev-parse", "HEAD"], TestContext.Current.CancellationToken);

        // Act
        var exception = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldStartWith("git rev-parse HEAD failed:", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Temporary_git_repository_commits_and_tags_without_the_user_configuration()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(ct);

        // Act
        await repository.Commit("file.txt", "content", "feat: add file", ct);
        await repository.Tag("v1.0.0", ct);

        // Assert
        (await repository.RunGit(["log", "-1", "--format=%s"], ct)).Trim().ShouldBe("feat: add file");
        (await repository.RunGit(["tag", "--list"], ct)).Trim().ShouldBe("v1.0.0");
    }

    [Fact]
    public static async Task Test_git_commit_all_commits_every_file_of_the_directory()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "a.txt"), "a", ct);

        // Act
        var commit = await TestGit.CommitAll(directory.Path, ct);

        // Assert
        (await TestGit.Run(directory.Path, ["rev-parse", "HEAD"], ct)).Trim().ShouldBe(commit);
        (await TestGit.Run(directory.Path, ["ls-files"], ct)).Trim().ShouldBe("a.txt");
    }

    [Fact]
    public static async Task Tracking_async_disposable_records_its_name_once_and_returns_the_configured_failure()
    {
        // Arrange
        List<string> order = [];
        var failure = new InvalidOperationException("dispose");
        var disposable = new TrackingAsyncDisposable("first", order, failure);

        // Act
        var first = disposable.DisposeAsync();
        await disposable.DisposeAsync();

        // Assert
        first.IsFaulted.ShouldBeTrue();
        order.ShouldBe(["first"]);
        disposable.DisposeCalled.ShouldBeTrue();
    }

    [Fact]
    public static async Task Tracking_cleanup_action_records_its_name_on_every_invocation()
    {
        // Arrange
        List<string> order = [];
        var action = new TrackingCleanupAction("cleanup", order);

        // Act
        await action.Invoke();
        await action.Invoke();

        // Assert
        order.ShouldBe(["cleanup", "cleanup"]);
    }
}
