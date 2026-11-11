using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

/// <summary>Tests Eventually, which retries an assertion until it passes or a timeout ends it, instead of a fixed sleep.</summary>
public sealed class EventuallyTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(5);

    [Fact]
    public async Task An_assertion_that_passes_on_a_later_attempt_succeeds_without_waiting_for_the_timeout()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var attempts = 0;

        // Act
        await Eventually.Succeeds(_ =>
        {
            attempts++;
            return attempts < 3 ? throw new InvalidOperationException("not yet") : Task.CompletedTask;
        }, TimeSpan.FromSeconds(30), Interval, ct);

        // Assert
        attempts.ShouldBe(3, "the assertion must run until it passes and then stop");
    }

    [Fact]
    public async Task An_assertion_that_never_passes_times_out_with_the_last_failure_as_the_inner_exception()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var attempts = 0;
        Func<Task> waiting = () => Eventually.Succeeds(_ =>
        {
            attempts++;
            throw new InvalidOperationException($"attempt {attempts}");
        }, TimeSpan.FromMilliseconds(200), Interval, ct);

        // Act
        var timeout = await waiting.ShouldThrow<TimeoutException>();

        // Assert
        timeout.InnerException.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe($"attempt {attempts}", "the last failure explains the timeout");
    }

    [Fact]
    public async Task An_attempt_that_hangs_is_cancelled_at_the_deadline()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Func<Task> waiting = () => Eventually.Succeeds(token => Task.Delay(Timeout.InfiniteTimeSpan, token), TimeSpan.FromMilliseconds(100), Interval, ct);

        // Act
        var timeout = await waiting.ShouldThrow<TimeoutException>();

        // Assert
        timeout.InnerException.ShouldBeNull("a hanging attempt produced no failure of its own");
    }

    [Fact]
    public async Task Cancellation_by_the_caller_is_not_reported_as_a_timeout()
    {
        // Arrange
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await caller.CancelAsync();
        Func<Task> waiting = () => Eventually.Succeeds(_ => Task.CompletedTask, TimeSpan.FromSeconds(30), caller.Token);

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
        Func<Task> zeroTimeout = () => Eventually.Succeeds(_ => Task.CompletedTask, TimeSpan.Zero, ct);
        Func<Task> zeroInterval = () => Eventually.Succeeds(_ => Task.CompletedTask, TimeSpan.FromSeconds(1), TimeSpan.Zero, ct);

        // Act
        var timeout = await zeroTimeout.ShouldThrow<ArgumentOutOfRangeException>();
        var interval = await zeroInterval.ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        timeout.ParamName.ShouldBe("timeout");
        interval.ParamName.ShouldBe("interval");
    }
}
