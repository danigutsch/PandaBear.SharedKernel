namespace PandaBear.Processes.Tests;

/// <summary>Tests the byte capture of the bounded runner with real child processes.</summary>
[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Trait(TestTraitNames.ScopeName, "unit")]
public sealed class BoundedProcessRunnerCaptureTests
{
    private static readonly TimeSpan StopLimit = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Standard_output_comes_back_byte_for_byte_with_nul_bytes_and_invalid_utf8()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var startInfo = ShellStartInfo.Create(@"printf 'a\000b\377'; printf 'warn' >&2; exit 4");

        // Act
        var result = await new BoundedProcessRunner().Capture(startInfo, ReadOnlyMemory<byte>.Empty, 1024, 1024, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(4);
        result.StandardOutput.ToArray().ShouldBe(new byte[] { (byte)'a', 0, (byte)'b', 0xFF });
        result.StandardError.ShouldBe("warn");
        startInfo.RedirectStandardInput.ShouldBeTrue("The runner writes and closes standard input.");
    }

    [Fact]
    public async Task Standard_input_reaches_the_process_and_is_closed()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        byte[] input = [0, 1, 2, 0xFF, (byte)'\n'];

        // Act
        var result = await new BoundedProcessRunner().Capture(ShellStartInfo.Create("cat"), input, 1024, 1024, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ToArray().ShouldBe(input);
    }

    [Fact]
    public async Task A_process_that_exits_without_reading_its_input_is_not_an_error()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var input = new byte[1024 * 1024];

        // Act
        var result = await new BoundedProcessRunner().Capture(ShellStartInfo.Create("exit 3"), input, 1024, 1024, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(3);
    }

    [Theory]
    [InlineData("exec yes")]
    [InlineData("exec yes >&2")]
    public async Task Output_above_a_limit_ends_the_process_with_an_error(string command)
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var processIdPath = Path.Combine(directory.Path, "pid");
        var startInfo = ShellStartInfo.Create($"printf '%s' \"$$\" > {FakeExecutable.Quote(processIdPath)}; {command}");

        // Act
        Func<Task> run = () => new BoundedProcessRunner().Capture(startInfo, ReadOnlyMemory<byte>.Empty, 4096, 4096, TestContext.Current.CancellationToken);

        // Assert
        var exception = await run.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe("The process wrote more output than the limit allows.");
        var stopped = await LinuxProcessState.WaitUntilStopped(ProcessIdFile.Read(processIdPath), StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

    [Fact]
    public async Task A_process_that_runs_past_the_time_limit_is_stopped_with_a_timeout()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var processIdPath = Path.Combine(directory.Path, "pid");
        var startInfo = ShellStartInfo.Create($"printf '%s' \"$$\" > {FakeExecutable.Quote(processIdPath)}; exec sleep 600");

        // Act
        Func<Task> run = () => new BoundedProcessRunner(TimeSpan.FromMilliseconds(500)).Capture(startInfo, ReadOnlyMemory<byte>.Empty, 1024, 1024, TestContext.Current.CancellationToken);

        // Assert
        var exception = await run.ShouldThrow<TimeoutException>();
        exception.Message.ShouldBe("The process did not finish within 0.5 seconds.");
        var stopped = await LinuxProcessState.WaitUntilStopped(ProcessIdFile.Read(processIdPath), StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

    [Fact]
    public async Task Cancellation_stops_the_process_and_carries_the_callers_token()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var processIdPath = Path.Combine(directory.Path, "pid");
        var startInfo = ShellStartInfo.Create($"printf '%s' \"$$\" > {FakeExecutable.Quote(processIdPath)}; exec sleep 600");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        Func<Task> run = () => new BoundedProcessRunner().Capture(startInfo, ReadOnlyMemory<byte>.Empty, 1024, 1024, cancellation.Token);

        // Assert
        var exception = await run.ShouldThrowAssignableTo<OperationCanceledException>();
        exception.CancellationToken.ShouldBe(cancellation.Token);
        var stopped = await LinuxProcessState.WaitUntilStopped(ProcessIdFile.Read(processIdPath), StopLimit, TestContext.Current.CancellationToken);
        stopped.ShouldBeTrue();
    }

    [Fact]
    public async Task Limits_the_time_limit_and_the_executable_are_checked_before_anything_starts()
    {
        // Arrange
        var startInfo = new System.Diagnostics.ProcessStartInfo("/bin/true");

        // Act
        Func<Task> noOutput = () => new BoundedProcessRunner().Capture(startInfo, ReadOnlyMemory<byte>.Empty, 0, 1024, TestContext.Current.CancellationToken);
        Func<Task> noError = () => new BoundedProcessRunner().Capture(startInfo, ReadOnlyMemory<byte>.Empty, 1024, 0, TestContext.Current.CancellationToken);
        Func<Task> relative = () => new BoundedProcessRunner().Capture(new System.Diagnostics.ProcessStartInfo("git"), ReadOnlyMemory<byte>.Empty, 1024, 1024, TestContext.Current.CancellationToken);

        // Assert
        await noOutput.ShouldThrow<ArgumentOutOfRangeException>();
        await noError.ShouldThrow<ArgumentOutOfRangeException>();
        await relative.ShouldThrow<ArgumentException>();
        Func<object?> noTime = () => new BoundedProcessRunner(TimeSpan.Zero);
        noTime.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
