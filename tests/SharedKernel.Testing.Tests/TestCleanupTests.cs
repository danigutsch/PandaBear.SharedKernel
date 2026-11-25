namespace SharedKernel.Testing.Tests;

/// <summary>
/// Tests that test cleanup attempts every resource or action in order and reports every failure: none, the only one as it is, or all of
/// them in an <see cref="AggregateException"/> with the operation failure first.
/// </summary>
public sealed class TestCleanupTests
{
    [Fact]
    public async Task Every_resource_is_disposed_in_order_and_null_ones_are_skipped()
    {
        // Arrange
        var order = new List<string>();
        var first = new TrackingAsyncDisposable("first", order);
        var second = new TrackingAsyncDisposable("second", order);

        // Act
        await TestCleanup.DisposeAll(null, first, null, second);

        // Assert
        order.ShouldBe(["first", "second"]);
    }

    [Fact]
    public async Task A_lone_cleanup_failure_is_rethrown_as_it_is_after_every_resource_ran()
    {
        // Arrange
        var order = new List<string>();
        var failure = new IOException("drop failed");
        await using var failing = new TrackingAsyncDisposable("failing", order, failure);
        await using var after = new TrackingAsyncDisposable("after", order);

        // Act
        Func<Task> cleanup = () => TestCleanup.DisposeAll(null, failing, after);
        var thrown = await cleanup.ShouldThrow<IOException>();

        // Assert
        thrown.ShouldBeSameAs(failure);
        order.ShouldBe(["failing", "after"]);
    }

    [Fact]
    public async Task A_lone_operation_failure_is_rethrown_as_it_is_after_a_clean_cleanup()
    {
        // Arrange
        var order = new List<string>();
        var operationFailure = new InvalidOperationException("setup failed");

        // Act
        Func<Task> cleanup = () => TestCleanup.DisposeAll(operationFailure, new TrackingAsyncDisposable("resource", order));
        var thrown = await cleanup.ShouldThrow<InvalidOperationException>();

        // Assert
        thrown.ShouldBeSameAs(operationFailure);
        order.ShouldBe(["resource"]);
    }

    [Fact]
    public async Task Several_failures_are_aggregated_with_the_operation_failure_first()
    {
        // Arrange
        var order = new List<string>();
        var operationFailure = new InvalidOperationException("setup failed");
        var firstFailure = new IOException("data source failed");
        var secondFailure = new TimeoutException("database drop timed out");

        // Act
        Func<Task> cleanup = () => TestCleanup.DisposeAll(
            operationFailure,
            new TrackingAsyncDisposable("data-source", order, firstFailure),
            new TrackingAsyncDisposable("database", order, secondFailure));
        var thrown = await cleanup.ShouldThrow<AggregateException>();

        // Assert
        thrown.InnerExceptions.ShouldBe([operationFailure, firstFailure, secondFailure]);
        order.ShouldBe(["data-source", "database"]);
    }

    [Fact]
    public async Task Cleanup_actions_are_run_like_resources()
    {
        // Arrange
        var order = new List<string>();
        var failure = new IOException("stop failed");
        var stop = new TrackingCleanupAction("stop", order, failure);
        var delete = new TrackingCleanupAction("delete", order);

        // Act
        Func<Task> cleanup = () => TestCleanup.Run(null, stop.Invoke, delete.Invoke);
        var thrown = await cleanup.ShouldThrow<IOException>();

        // Assert
        thrown.ShouldBeSameAs(failure);
        order.ShouldBe(["stop", "delete"]);
    }

    [Fact]
    public async Task An_action_that_throws_instead_of_returning_a_faulted_task_is_reported_too()
    {
        // Arrange
        var failure = new InvalidOperationException("thrown synchronously");
        var ran = false;

        // Act
        Func<Task> cleanup = () => TestCleanup.Run(
            null,
            () => throw failure,
            () =>
            {
                ran = true;
                return ValueTask.CompletedTask;
            });
        var thrown = await cleanup.ShouldThrow<InvalidOperationException>();

        // Assert
        thrown.ShouldBeSameAs(failure);
        ran.ShouldBeTrue();
    }

    [Fact]
    public async Task Nothing_is_thrown_when_everything_succeeds()
    {
        // Arrange
        var order = new List<string>();

        // Act
        var thrown = await Record.ExceptionAsync(() => TestCleanup.DisposeAll(null, new TrackingAsyncDisposable("only", order)));

        // Assert
        thrown.ShouldBeNull();
        order.ShouldBe(["only"]);
    }
}
