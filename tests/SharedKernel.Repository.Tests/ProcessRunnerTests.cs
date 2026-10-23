using System.Diagnostics;
using PandaBear.Repository.Tool;
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
        var (fileName, arguments) = TestShell.Command(
            "echo $$ > child.pid; exec sleep 60",
            "Set-Content -LiteralPath child.pid -Value $PID; Start-Sleep -Seconds 60");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = ProcessRunner.Run(fileName, arguments, directory.Path, cancellation.Token);
        var processId = await ChildProcessId.Read(Path.Combine(directory.Path, "child.pid"), running, TestContext.Current.CancellationToken);

        // Act
        await cancellation.CancelAsync();
        var action = () => running.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await action.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        var lookup = () => Process.GetProcessById(processId);
        lookup.ShouldThrow<ArgumentException>();
    }
}
