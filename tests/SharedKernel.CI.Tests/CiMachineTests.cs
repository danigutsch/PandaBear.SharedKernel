using System.Diagnostics;
using PandaBear.Processes;

namespace PandaBear.CI.Tests;

/// <summary>Guards how the CI engine runs child processes: output capture, failures, cancellation, timeouts, and streaming.</summary>
public sealed class CiMachineTests
{
    private static readonly CiMachine Machine = new(new SystemProcessRunner(), PathExecutableResolver.Default, TimeProvider.System);

    [Fact]
    public async Task Completed_process_returns_its_standard_output()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var (fileName, arguments) = TestShell.Command("echo selected", "Write-Output selected");

        // Act
        var output = await Machine.Run(fileName, arguments, directory.Path, TestContext.Current.CancellationToken);

        // Assert
        output.Trim().ShouldBe("selected");
    }

    [Fact]
    public async Task Streaming_a_process_still_returns_its_standard_output()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var (fileName, arguments) = TestShell.Command("echo selected", "Write-Output selected");

        // Act
        using var echo = new InvariantStringWriter();
        var output = await Machine.RunStreaming(fileName, arguments, directory.Path, echo, TestContext.Current.CancellationToken);

        // Assert
        output.Trim().ShouldBe("selected");
        echo.ToString().ShouldMatch(@"\A\[\d\d:\d\d:\d\d\] selected\r?\n\z");
    }

    [Fact]
    public async Task Failed_process_reports_its_exit_code_and_standard_error()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var (fileName, arguments) = TestShell.Command("echo failure-detail >&2; exit 3", "[Console]::Error.WriteLine('failure-detail'); exit 3");
        var action = () => Machine.Run(fileName, arguments, directory.Path, TestContext.Current.CancellationToken);

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
        var action = () => Machine.Run("sharedkernel-missing-executable", [], directory.Path, TestContext.Current.CancellationToken);

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
        var running = Machine.Run(fileName, arguments, Path.GetTempPath(), cancellation.Token);
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
    public async Task Timed_out_process_is_terminated_and_reported_as_a_timeout()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var processIdPath = Path.Combine(directory.Path, "child.pid");
        var bashPath = processIdPath.Replace("'", "'\\''", StringComparison.Ordinal);
        var powerShellPath = processIdPath.Replace("'", "''", StringComparison.Ordinal);
        var (fileName, arguments) = TestShell.Command(
            $"echo $$ > '{bashPath}'; exec sleep 60",
            $"Set-Content -LiteralPath '{powerShellPath}' -Value $PID; Start-Sleep -Seconds 60");
        var timeProvider = new ManualTimeProvider();
        var running = new CiMachine(new SystemProcessRunner(), PathExecutableResolver.Default, timeProvider).Run(fileName, arguments, Path.GetTempPath(), TimeSpan.FromMinutes(10), static _ => { }, null, TestContext.Current.CancellationToken);
        var processId = await ChildProcessId.Read(processIdPath, running, TestContext.Current.CancellationToken);

        // Act
        // The timeout fires only after the child has reported its process ID, however long its shell took to start.
        timeProvider.Fire();
        var action = () => running.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldContain("did not exit within 600 seconds", StringComparison.Ordinal);
        var lookup = () => Process.GetProcessById(processId);
        lookup.ShouldThrow<ArgumentException>();
    }
}
