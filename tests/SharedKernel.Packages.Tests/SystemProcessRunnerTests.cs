using System.Diagnostics;

namespace PandaBear.Packages.Tests;

/// <summary>Tests the real process runner with small Unix tools.</summary>
[Trait("Capability", "Packages")]
public sealed class SystemProcessRunnerTests
{
    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = "The Unix tools are not available.")]
    public async Task Output_error_output_and_exit_code_are_returned()
    {
        // Act
        var output = await new SystemProcessRunner().Run(UnixToolsProbe.Shell, ["-c", "echo out; echo warn >&2; exit 3"], TestContext.Current.CancellationToken);

        // Assert
        output.ExitCode.ShouldBe(3);
        output.StandardOutput.ShouldBe("out\n");
        output.StandardError.ShouldBe("warn\n");
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = "The Unix tools are not available.")]
    public async Task Endless_output_ends_the_process_with_an_output_limit_error_instead_of_waiting_for_the_timeout()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act
        Func<Task> act = () => new SystemProcessRunner().Run(UnixToolsProbe.Yes, [], TestContext.Current.CancellationToken);

        // Assert
        var exception = await act.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("more output than the limit", StringComparison.Ordinal);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(60), "The limit must end the process well before the two-minute timeout.");
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = "The Unix tools are not available.")]
    public async Task Output_a_little_over_the_limit_from_a_process_that_then_exits_is_still_an_error_not_an_empty_success()
    {
        // Arrange: the 8 MiB limit is crossed in the last chunk, after which the process exits by itself.
        const string script = "yes | head -c 8388700";

        // Act
        Func<Task> act = () => new SystemProcessRunner().Run(UnixToolsProbe.Shell, ["-c", script], TestContext.Current.CancellationToken);

        // Assert
        var exception = await act.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("more output than the limit", StringComparison.Ordinal);
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = "The Unix tools are not available.")]
    public async Task Endless_error_output_is_limited_too()
    {
        // Act
        Func<Task> act = () => new SystemProcessRunner().Run(UnixToolsProbe.Shell, ["-c", "yes >&2"], TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrow<InvalidOperationException>();
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = "The Unix tools are not available.")]
    public async Task Cancellation_stops_a_running_process_and_throws_a_cancellation()
    {
        // Arrange
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var stopwatch = Stopwatch.StartNew();
        source.CancelAfter(TimeSpan.FromMilliseconds(300));

        // Act
        Func<Task> act = () => new SystemProcessRunner().Run(UnixToolsProbe.Shell, ["-c", "sleep 60"], source.Token);

        // Assert
        await act.ShouldThrowAssignableTo<OperationCanceledException>();
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task A_program_that_does_not_exist_is_an_invalid_operation()
    {
        // Act
        Func<Task> act = () => new SystemProcessRunner().Run("/nonexistent/definitely-not-a-tool", [], TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrow<InvalidOperationException>();
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = "The Unix tools are not available.")]
    public async Task Standard_input_is_closed_so_a_tool_that_reads_it_ends_at_once()
    {
        // Act
        var output = await new SystemProcessRunner().Run(UnixToolsProbe.Shell, ["-c", "read line; echo done"], TestContext.Current.CancellationToken);

        // Assert
        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldBe("done\n");
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = "The Unix tools are not available.")]
    public async Task The_tool_runs_in_the_c_locale()
    {
        // Act
        var output = await new SystemProcessRunner().Run(UnixToolsProbe.Shell, ["-c", "printf %s \"$LC_ALL\""], TestContext.Current.CancellationToken);

        // Assert
        output.StandardOutput.ShouldBe("C");
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = "The Unix tools are not available.")]
    public async Task The_tool_sees_every_locale_variable_as_c_and_no_language_override()
    {
        // Act
        var output = await new SystemProcessRunner().Run(UnixToolsProbe.Shell, ["-c", "printf '%s|%s|%s|%s' \"$LC_ALL\" \"$LC_MESSAGES\" \"$LANG\" \"${LANGUAGE-unset}\""], TestContext.Current.CancellationToken);

        // Assert
        output.StandardOutput.ShouldBe("C|C|C|unset");
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = "The Unix tools are not available.")]
    public async Task Arguments_are_passed_one_by_one_without_shell_interpretation()
    {
        // Act
        var output = await new SystemProcessRunner().Run(UnixToolsProbe.Shell, ["-c", "printf %s \"$1\"", "name", "a b;touch x $(echo y)"], TestContext.Current.CancellationToken);

        // Assert
        output.StandardOutput.ShouldBe("a b;touch x $(echo y)");
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = "The Unix tools are not available.")]
    public async Task Cancellation_kills_the_process_instead_of_leaving_it_running()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var pidFile = Path.Combine(directory.Path, "pid");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = new SystemProcessRunner().Run(UnixToolsProbe.Shell, ["-c", "echo $$ > \"$1\"; exec sleep 60", "name", pidFile], source.Token);
        var processId = await RunningProcessFile.WaitForProcessId(pidFile, TestContext.Current.CancellationToken);

        // Act
        await source.CancelAsync();
        Func<Task> act = () => run;

        // Assert
        await act.ShouldThrowAssignableTo<OperationCanceledException>();
        RunningProcessFile.IsRunning(processId).ShouldBeFalse("The killed process must be gone, not left to sleep.");
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = "The Unix tools are not available.")]
    public async Task A_background_process_that_keeps_the_output_open_after_the_tool_exits_is_an_error_not_a_hang()
    {
        // Act
        Func<Task> act = () => new SystemProcessRunner().Run(UnixToolsProbe.Shell, ["-c", "sleep 9 & exit 0"], TestContext.Current.CancellationToken);

        // Assert
        var exception = await act.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("output open", StringComparison.Ordinal);
    }
}
