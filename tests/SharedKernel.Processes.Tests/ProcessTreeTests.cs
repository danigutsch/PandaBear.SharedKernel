using System.Diagnostics;
using System.Globalization;

namespace PandaBear.Processes.Tests;

/// <summary>Tests starting a process and ending its process tree.</summary>
public sealed class ProcessTreeTests
{
    [Fact]
    public void A_process_that_cannot_start_is_reported_as_an_invalid_operation()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var missing = Path.Combine(directory.Path, "missing-sharedkernel-command");

        // Act
        Action start = () => ProcessTree.Start(new ProcessStartInfo(missing) { UseShellExecute = false });

        // Assert
        var exception = start.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe($"Failed to start {missing}: {exception.InnerException.ShouldNotBeNull().Message}");
    }

    [Fact]
    public async Task Termination_kills_the_whole_tree()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var startInfo = ShellStartInfo.Create("sleep 60 & echo $!; wait");
        startInfo.RedirectStandardOutput = true;
        startInfo.UseShellExecute = false;
        using var process = ProcessTree.Start(startInfo);
        var line = await process.StandardOutput.ReadLineAsync(TestContext.Current.CancellationToken);
        var childId = int.Parse(line!, CultureInfo.InvariantCulture);

        // Act
        await ProcessTree.Terminate(process, TimeSpan.FromSeconds(10));

        // Assert
        var childStopped = await LinuxProcessState.WaitUntilStopped(childId, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        process.HasExited.ShouldBeTrue();
        childStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task Termination_ignores_a_process_object_without_a_running_process()
    {
        // Arrange
        using Process process = new();

        // Act
        var exception = await Record.ExceptionAsync(() => ProcessTree.Terminate(process, TimeSpan.FromSeconds(2)));

        // Assert
        exception.ShouldBeNull();
    }

    [Fact]
    public async Task Termination_stops_waiting_when_a_killed_process_never_reports_its_exit()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var unreaped = await UnreapedProcess.Start(TestContext.Current.CancellationToken);

        // Act
        await ProcessTree.Terminate(unreaped.Process, TimeSpan.FromSeconds(2)).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Assert
        unreaped.IsZombie.ShouldBeTrue();
        unreaped.Process.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        // Act
        Action start = () => ProcessTree.Start(null!);
        Func<Task> terminateMissing = () => ProcessTree.Terminate(null!, TimeSpan.Zero);

        // Assert
        start.ShouldThrow<ArgumentNullException>();
        await terminateMissing.ShouldThrow<ArgumentNullException>();
    }
}
