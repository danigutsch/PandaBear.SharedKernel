using System.Diagnostics;
using System.Text;

namespace PandaBear.Secrets.Tests;

/// <summary>Tests the real process runner with small Unix tools.</summary>
[Trait("Capability", "Secrets")]
public sealed class SystemToolRunnerTests
{
    private const string SkipReason = "The Unix tools are not available.";

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = SkipReason)]
    public async Task Standard_input_reaches_the_process_and_its_output_comes_back_as_bytes()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("secret-on-stdin");

        // Act
        using var result = await new SystemToolRunner().Run(new ToolRequest(UnixToolsProbe.Cat, [], standardInput: input), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.RevealUtf8().ShouldBe("secret-on-stdin");
        result.StandardError.ShouldBe(string.Empty);
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = SkipReason)]
    public async Task Large_input_and_output_do_not_deadlock()
    {
        // Arrange
        var input = new byte[3 * 1024 * 1024];
        Array.Fill(input, (byte)'x');

        // Act
        using var result = await new SystemToolRunner().Run(new ToolRequest(UnixToolsProbe.Cat, [], standardInput: input), TestContext.Current.CancellationToken);

        // Assert
        result.StandardOutput.Length.ShouldBe(input.Length);
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = SkipReason)]
    public async Task The_child_inherits_no_environment_except_the_request_and_the_c_locale()
    {
        // Arrange
        var request = new ToolRequest(UnixToolsProbe.Env, [], new Dictionary<string, string> { ["ONLY_THIS"] = "value" });

        // Act
        using var result = await new SystemToolRunner().Run(request, TestContext.Current.CancellationToken);

        // Assert
        result.StandardOutput.RevealUtf8().Split('\n', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToArray().ShouldBe(["LC_ALL=C", "ONLY_THIS=value"]);
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = SkipReason)]
    public async Task The_exit_code_and_error_text_are_returned()
    {
        // Act
        using var result = await new SystemToolRunner().Run(new ToolRequest(UnixToolsProbe.Shell, ["-c", "echo out; echo warn >&2; exit 3"]), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(3);
        result.StandardOutput.RevealUtf8().ShouldBe("out\n");
        result.StandardError.ShouldBe("warn\n");
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = SkipReason)]
    public async Task A_process_that_ignores_its_input_and_exits_is_not_an_error()
    {
        // Arrange
        var input = new byte[4 * 1024 * 1024];

        // Act
        using var result = await new SystemToolRunner().Run(new ToolRequest(UnixToolsProbe.Shell, ["-c", "exit 0"], standardInput: input), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = SkipReason)]
    public async Task Endless_output_ends_the_process_with_an_output_limit_error()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act
        Func<Task> act = () => new SystemToolRunner().Run(new ToolRequest(UnixToolsProbe.Yes, []), TestContext.Current.CancellationToken);

        // Assert
        var exception = await act.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("more output than the limit", StringComparison.Ordinal);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(60));
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = SkipReason)]
    public async Task Endless_error_output_is_limited_too()
    {
        // Act
        Func<Task> act = () => new SystemToolRunner().Run(new ToolRequest(UnixToolsProbe.Shell, ["-c", "yes >&2"]), TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrow<InvalidOperationException>();
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = SkipReason)]
    public async Task A_process_that_runs_too_long_is_stopped_with_a_timeout()
    {
        // Act
        Func<Task> act = () => new SystemToolRunner(TimeSpan.FromMilliseconds(300)).Run(new ToolRequest(UnixToolsProbe.Shell, ["-c", "sleep 30"]), TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrow<TimeoutException>();
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = SkipReason)]
    public async Task Cancellation_stops_the_process_and_throws()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        Func<Task> act = () => new SystemToolRunner().Run(new ToolRequest(UnixToolsProbe.Shell, ["-c", "sleep 30"]), cancellation.Token);

        // Assert
        await act.ShouldThrowAssignableTo<OperationCanceledException>();
    }

    [Fact(SkipType = typeof(UnixToolsProbe), SkipUnless = nameof(UnixToolsProbe.IsAvailable), Skip = SkipReason)]
    public async Task A_program_that_does_not_exist_is_an_invalid_operation()
    {
        // Act
        Func<Task> act = () => new SystemToolRunner().Run(new ToolRequest("/nonexistent/program", []), TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public async Task The_time_limit_must_be_positive_and_the_request_is_required()
    {
        // Act
        Action zero = () => _ = new SystemToolRunner(TimeSpan.Zero);
        Func<Task> noRequest = () => new SystemToolRunner().Run(null!, TestContext.Current.CancellationToken);

        // Assert
        zero.ShouldThrow<ArgumentOutOfRangeException>();
        await noRequest.ShouldThrow<ArgumentNullException>();
    }
}
