using System.Diagnostics;
using System.Text;

namespace SharedKernel.DevContainers.Tests;

/// <summary>Engine tests split from the command-line tests in PodmanDnsPreflightCommandTests.</summary>
public sealed class PodmanDnsPreflightTests
{
    [Fact]
    public async Task Process_start_failure_names_the_executable()
    {
        // Arrange
        var executable = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-command");
        ProcessStartInfo startInfo = new(executable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        Func<Task> run = () => PodmanDnsPreflight.RunProcess(startInfo, TestContext.Current.CancellationToken);

        // Act
        var exception = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain(executable, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Process_cleanup_ignores_an_already_unavailable_process()
    {
        // Arrange
        using Process process = new();
        var completed = false;

        // Act
        await PodmanDnsPreflight.Stop(process);
        completed = true;

        // Assert
        completed.ShouldBe(true);
    }

    [Fact]
    public async Task Cancellation_kills_the_inspection_process_tree()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        ProcessStartInfo startInfo = new("/bin/sh")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = directory.Path
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("sleep 60 & echo $! > grandchild.pid; wait");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = PodmanDnsPreflight.RunProcess(startInfo, cancellation.Token);
        var grandchildId = await ChildProcessId.Read(
            Path.Combine(directory.Path, "grandchild.pid"),
            running,
            TestContext.Current.CancellationToken);
        var grandchild = LinuxProcessIdentity.Capture(grandchildId);

        // Act
        await cancellation.CancelAsync();
        var action = () => running.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await action.ShouldThrowAssignableTo<OperationCanceledException>();
        var exited = await grandchild.WaitForExit(TestContext.Current.CancellationToken);

        // Assert
        exited.ShouldBeTrue();
    }

    [Fact]
    public async Task Process_output_capture_retains_only_the_limit_marker()
    {
        // Arrange
        var oversizedOutput = new string('x', 1_048_577) + "discarded";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(oversizedOutput));
        using var reader = new StreamReader(stream, Encoding.UTF8);

        // Act
        var capturedOutput = await PodmanDnsPreflight.ReadBoundedOutput(
            reader,
            TestContext.Current.CancellationToken);

        // Assert
        capturedOutput.Length.ShouldBe(1_048_577);
    }
}
