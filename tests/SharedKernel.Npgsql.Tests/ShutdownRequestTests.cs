using System.Runtime.InteropServices;
using PandaBear.Npgsql.Testing.Host;

namespace PandaBear.Npgsql.Tests;

public sealed class ShutdownRequestTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task The_end_of_standard_input_requests_the_stop_and_data_on_it_does_not()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var input = new ScriptedInput([1, 2, 3]);
        using var request = new ShutdownRequest(input, []);
        await input.Waiting.WaitAsync(Patience, ct);
        var requestedWhileInputWasOpen = request.Token.IsCancellationRequested;

        // Act
        input.End();
        await request.Wait().WaitAsync(Patience, ct);

        // Assert
        requestedWhileInputWasOpen.ShouldBeFalse("input is not a command, so a line typed into a terminal must not stop the host");
        request.Token.IsCancellationRequested.ShouldBeTrue("the owner closing the pipe, or dying, ends the input");
        request.Reason.ShouldBe("standard input reached end-of-file");
    }

    [Theory]
    [InlineData(PosixSignal.SIGTERM)]
    [InlineData(PosixSignal.SIGINT)]
    [InlineData(PosixSignal.SIGHUP)]
    public async Task A_termination_signal_requests_the_stop_and_keeps_the_runtime_from_ending_the_process(PosixSignal signal)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var input = new ScriptedInput();
        using var request = new ShutdownRequest(input, []);
        var context = new PosixSignalContext(signal);

        // Act
        request.OnSignal(context);
        await request.Wait().WaitAsync(Patience, ct);

        // Assert
        context.Cancel.ShouldBeTrue("the host stops the server itself; the default action would end the process with the container still running");
        request.Token.IsCancellationRequested.ShouldBeTrue();
        request.Reason.ShouldBe(signal.ToString());
    }

    [Fact]
    public async Task The_first_request_names_the_reason_and_a_later_one_changes_nothing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var input = new ScriptedInput();
        using var request = new ShutdownRequest(input, []);
        request.OnSignal(new PosixSignalContext(PosixSignal.SIGTERM));

        // Act
        request.OnSignal(new PosixSignalContext(PosixSignal.SIGHUP));
        input.End();
        await request.Wait().WaitAsync(Patience, ct);

        // Assert
        request.Reason.ShouldBe("SIGTERM", "the log must name what stopped the host, not what arrived while it was already stopping");
    }

    [Fact]
    public void The_host_stops_on_the_signals_that_a_terminal_a_service_manager_and_a_closed_session_send()
    {
        // Act
        var names = ShutdownRequest.TerminationSignals.Select(signal => signal.ToString()).Order(StringComparer.Ordinal).ToArray();

        // Assert
        names.ShouldBe(["SIGHUP", "SIGINT", "SIGTERM"], "the owner host protocol names SIGTERM, SIGINT and SIGHUP");
    }
}
