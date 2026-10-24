using System.Diagnostics;
using PandaBear.CI.Tool;
using PandaBear.Testing;

namespace PandaBear.Repository.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task Completed_process_returns_its_standard_output()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var (fileName, arguments) = TestShell.Command("echo selected", "Write-Output selected");

        // Act
        var output = await ProcessRunner.Run(fileName, arguments, directory.Path, TestContext.Current.CancellationToken);

        // Assert
        output.Trim().ShouldBe("selected");
    }

    [Fact]
    public async Task Failed_process_reports_its_exit_code_and_standard_error()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var (fileName, arguments) = TestShell.Command("echo failure-detail >&2; exit 3", "[Console]::Error.WriteLine('failure-detail'); exit 3");
        var action = () => ProcessRunner.Run(fileName, arguments, directory.Path, TestContext.Current.CancellationToken);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("(3)", StringComparison.Ordinal);
        exception.Message.ShouldContain("failure-detail", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_executable_fails_as_an_invalid_operation()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var action = () => ProcessRunner.Run("sharedkernel-missing-executable", [], directory.Path, TestContext.Current.CancellationToken);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("sharedkernel-missing-executable", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_terminates_the_running_process()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var processIdPath = Path.Combine(directory.Path, "child.pid");
        var bashPath = processIdPath.Replace("'", "'\\''", StringComparison.Ordinal);
        var powerShellPath = processIdPath.Replace("'", "''", StringComparison.Ordinal);
        var (fileName, arguments) = TestShell.Command(
            $"echo $$ > '{bashPath}'; exec sleep 60",
            $"Set-Content -LiteralPath '{powerShellPath}' -Value $PID; Start-Sleep -Seconds 60");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        // Waiting for the shell does not guarantee that every Windows descendant has exited.
        // Keep its working directory outside the fixture; the PID file still proves the child started.
        var running = ProcessRunner.Run(fileName, arguments, Path.GetTempPath(), cancellation.Token);
        var processId = await ChildProcessId.Read(processIdPath, running, TestContext.Current.CancellationToken);

        // Act
        await cancellation.CancelAsync();
        var action = () => running.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await action.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        var lookup = () => Process.GetProcessById(processId);
        lookup.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public async Task Termination_ignores_a_process_object_without_a_running_process()
    {
        // Arrange
        using Process process = new();

        // Act
        var exception = await Record.ExceptionAsync(() => ProcessRunner.Terminate(process));

        // Assert
        exception.ShouldBeNull();
    }
}
