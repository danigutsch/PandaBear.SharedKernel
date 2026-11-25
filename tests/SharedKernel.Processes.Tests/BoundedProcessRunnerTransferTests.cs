using System.Diagnostics;

namespace SharedKernel.Processes.Tests;

/// <summary>Tests the process lifetime when output is transferred to separate caller streams.</summary>
[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Trait(TestTraitNames.ScopeName, "unit")]
public sealed class BoundedProcessRunnerTransferTests
{
    [Fact]
    public async Task A_write_failure_stops_the_child_before_the_process_deadline()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        using var output = new TestProcessOutputStream(() => throw new IOException("The test output destination failed."));
        using var error = new MemoryStream();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var pidFile = Path.Combine(directory.Path, "pid");
        var startInfo = ShellStartInfo.Create($"printf '%s' \"$$\" > {FakeExecutable.Quote(pidFile)}; printf 'ready'; exec sleep 600");

        // Act
        Func<Task> wait = async () => await new BoundedProcessRunner().RunToStreams(startInfo, output, 1024, error, 1024, deadline.Token);

        // Assert
        var failure = await wait.ShouldThrow<IOException>();
        failure.Message.ShouldBe("The test output destination failed.");
        var stopped = await LinuxProcessState.WaitUntilStopped(
            ProcessIdFile.Read(pidFile), TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
        output.CanWrite.ShouldBeTrue();
        error.CanWrite.ShouldBeTrue();
    }

    [Fact]
    public async Task Caller_cancellation_stops_the_child_and_preserves_already_written_bytes()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var output = new TestProcessOutputStream(cancellation.Cancel);
        using var error = new MemoryStream();
        var pidFile = Path.Combine(directory.Path, "pid");
        var startInfo = ShellStartInfo.Create($"printf '%s' \"$$\" > {FakeExecutable.Quote(pidFile)}; printf 'A'; exec sleep 600");
        startInfo.RedirectStandardInput = true;

        // Act
        Func<Task> wait = async () => await new BoundedProcessRunner().RunToStreams(startInfo, output, 1024, error, 1024, cancellation.Token);

        // Assert
        var exception = await wait.ShouldThrowAssignableTo<OperationCanceledException>();
        exception.CancellationToken.ShouldBe(cancellation.Token);
        output.ToArray().ShouldBe(new byte[] { (byte)'A' });
        var stopped = await LinuxProcessState.WaitUntilStopped(
            ProcessIdFile.Read(pidFile), TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

    [Fact]
    public async Task Destination_streams_must_be_separate_and_writable()
    {
        // Arrange
        var startInfo = new ProcessStartInfo(Path.GetFullPath("never-started"));
        using var output = new MemoryStream();
        using var error = new MemoryStream([], writable: false);
        var runner = new BoundedProcessRunner();

        // Act
        var ct = TestContext.Current.CancellationToken;
        Func<Task> shared = async () => await runner.RunToStreams(startInfo, output, 1024, output, 1024, ct);
        Func<Task> readOnly = async () => await runner.RunToStreams(startInfo, output, 1024, error, 1024, ct);

        // Assert
        await shared.ShouldThrow<ArgumentException>();
        await readOnly.ShouldThrow<ArgumentException>();
    }

}
