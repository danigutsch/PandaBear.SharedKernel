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
    public async Task A_failed_read_of_standard_input_requests_the_stop_instead_of_ending_the_process()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var input = new ScriptedInput();
        using var request = new ShutdownRequest(input, []);
        await input.Waiting.WaitAsync(Patience, ct);

        // Act
        // A terminal that hangs up fails the blocked read; an exception on the reader thread would end the process before the server stopped.
        input.Fail(new IOException("Input/output error"));
        await request.Wait().WaitAsync(Patience, ct);

        // Assert
        request.Token.IsCancellationRequested.ShouldBeTrue("a hangup of the terminal that feeds standard input stops the host, as SIGHUP does");
        request.Reason.ShouldBe("standard input could not be read");
    }

    [Fact]
    public void A_repeated_signal_lets_the_runtime_end_the_process_because_the_graceful_stop_is_stuck_or_too_slow()
    {
        // Arrange
        using var input = new ScriptedInput();
        using var request = new ShutdownRequest(input, []);
        var first = new PosixSignalContext(PosixSignal.SIGINT);
        var second = new PosixSignalContext(PosixSignal.SIGINT);

        // Act
        request.OnSignal(first);
        request.OnSignal(second);

        // Assert
        first.Cancel.ShouldBeTrue("the first signal starts a graceful stop, which the default action would cut short");
        second.Cancel.ShouldBeFalse("a second signal means that the stop does not finish; the process then ends and DCP removes the container");
    }

    [Theory]
    [InlineData(false, "standard input reached end-of-file")]
    [InlineData(true, "standard input could not be read")]
    public async Task The_first_signal_still_gets_a_graceful_stop_when_the_input_already_ended_or_failed(bool inputFails, string reason)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var input = new ScriptedInput();
        using var request = new ShutdownRequest(input, []);
        if (inputFails)
        {
            input.Fail(new IOException("Input/output error"));
        }
        else
        {
            input.End();
        }

        await request.Wait().WaitAsync(Patience, ct);
        var signal = new PosixSignalContext(PosixSignal.SIGHUP);

        // Act
        request.OnSignal(signal);

        // Assert
        signal.Cancel.ShouldBeTrue("a hangup fails standard input and sends SIGHUP, and the owner closing standard input is no impatient second request");
        request.Reason.ShouldBe(reason);
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
