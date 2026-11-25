using SharedKernel.IntegrationTesting;

namespace SharedKernel.Npgsql.Tests;

/// <summary>Tests Eventually.Until, which polls a probe until it returns a value (a row another process writes, a message that arrives).</summary>
public sealed class EventuallyUntilTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(5);

    [Fact]
    public async Task The_first_value_the_probe_returns_is_returned_without_waiting_for_the_timeout()
    {
        // Arrange
        var attempts = 0;

        // Act
        var value = await Eventually.Until(_ =>
        {
            attempts++;
            return Task.FromResult(attempts < 3 ? null : "found");
        }, TimeSpan.FromSeconds(30), Interval, TestContext.Current.CancellationToken);

        // Assert
        value.ShouldBe("found");
        attempts.ShouldBe(3, "the probe must run until it returns a value and then stop");
    }

    [Fact]
    public async Task A_probe_that_never_returns_a_value_times_out_with_the_attempt_count()
    {
        // Arrange
        Func<Task> waiting = () => Eventually.Until(_ => Task.FromResult<string?>(null), TimeSpan.FromMilliseconds(100), Interval, TestContext.Current.CancellationToken);

        // Act
        var timeout = await waiting.ShouldThrow<TimeoutException>();

        // Assert
        timeout.Message.ShouldContain("attempts", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_probe_that_hangs_is_cancelled_at_the_deadline_and_reported_as_a_timeout()
    {
        // Arrange
        Func<Task> waiting = () => Eventually.Until<string>(
            async token =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return null;
            },
            TimeSpan.FromMilliseconds(100),
            Interval,
            TestContext.Current.CancellationToken);

        // Act
        var timeout = await waiting.ShouldThrow<TimeoutException>();

        // Assert
        timeout.InnerException.ShouldBeNull("the deadline is the timeout, not a failure of the probe");
    }

    [Fact]
    public async Task A_probe_that_fails_stops_the_wait_with_its_exception()
    {
        // Arrange
        var attempts = 0;
        Func<Task> waiting = () => Eventually.Until<string>(_ =>
        {
            attempts++;
            throw new InvalidOperationException("broken probe");
        }, TimeSpan.FromSeconds(30), Interval, TestContext.Current.CancellationToken);

        // Act
        var failure = await waiting.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldBe("broken probe");
        attempts.ShouldBe(1, "a broken probe is a bug, not a state that is not reached yet");
    }

    [Fact]
    public async Task Cancellation_by_the_caller_is_not_reported_as_a_timeout()
    {
        // Arrange
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await caller.CancelAsync();
        Func<Task> waiting = () => Eventually.Until(_ => Task.FromResult<string?>("found"), TimeSpan.FromSeconds(30), caller.Token);

        // Act
        var cancelled = await waiting.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        cancelled.CancellationToken.ShouldBe(caller.Token);
    }

    [Fact]
    public async Task A_non_positive_timeout_or_interval_is_refused()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Func<Task> zeroTimeout = () => Eventually.Until(_ => Task.FromResult<string?>("found"), TimeSpan.Zero, ct);
        Func<Task> zeroInterval = () => Eventually.Until(_ => Task.FromResult<string?>("found"), TimeSpan.FromSeconds(1), TimeSpan.Zero, ct);

        // Act
        var timeout = await zeroTimeout.ShouldThrow<ArgumentOutOfRangeException>();
        var interval = await zeroInterval.ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        timeout.ParamName.ShouldBe("timeout");
        interval.ParamName.ShouldBe("interval");
    }
}
