namespace PandaBear.Npgsql.Tests;

/// <summary>Tests how PostgreSqlTestCleanup combines or rethrows operation and cleanup failures.</summary>
public sealed class PostgreSqlTestCleanupFailureTests
{
    [Fact]
    public async Task Setup_failure_cleanup_attempts_every_action_and_preserves_failures()
    {
        // Arrange
        var operationFailure = new InvalidOperationException("setup failed");
        var cleanupFailure = new IOException("cleanup failed");
        var cleanupOrder = new List<string>();
        var failing = new TrackingCleanupAction("first", cleanupOrder, cleanupFailure);
        var succeeding = new TrackingCleanupAction("second", cleanupOrder);

        // Act
        Func<Task> cleanup = () => PostgreSqlTestCleanup.Run(
            operationFailure,
            failing.Invoke,
            succeeding.Invoke);
        var exception = await cleanup.ShouldThrow<AggregateException>();

        // Assert
        cleanupOrder.ShouldBe(["first", "second"], "an action after a failing one still runs, in the given order");
        exception.InnerExceptions.Count.ShouldBe(2);
        exception.InnerExceptions[0].ShouldBeSameAs(operationFailure);
        exception.InnerExceptions[1].ShouldBeSameAs(cleanupFailure);
    }

    [Fact]
    public async Task A_single_cleanup_failure_is_rethrown_as_is()
    {
        // Arrange
        var cleanupFailure = new IOException("cleanup failed");
        var cleanupOrder = new List<string>();
        var failing = new TrackingCleanupAction("failing", cleanupOrder, cleanupFailure);
        var succeeding = new TrackingCleanupAction("succeeding", cleanupOrder);

        // Act
        Func<Task> cleanup = () => PostgreSqlTestCleanup.Run(
            operationFailure: null,
            failing.Invoke,
            succeeding.Invoke);
        var exception = await cleanup.ShouldThrow<IOException>();

        // Assert
        exception.ShouldBeSameAs(cleanupFailure, "a lone failure is not wrapped, so callers can still match its type");
        cleanupOrder.ShouldBe(["failing", "succeeding"], "the action after the failing one still runs");
    }

    [Fact]
    public async Task A_lone_operation_failure_is_rethrown_as_is_after_successful_cleanup()
    {
        // Arrange
        var operationFailure = new InvalidOperationException("setup failed");
        var cleanupOrder = new List<string>();
        var succeeding = new TrackingCleanupAction("succeeding", cleanupOrder);

        // Act
        Func<Task> cleanup = () => PostgreSqlTestCleanup.Run(operationFailure, succeeding.Invoke);
        var exception = await cleanup.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.ShouldBeSameAs(operationFailure, "the operation failure is not wrapped when cleanup succeeds");
        cleanupOrder.ShouldBe(["succeeding"], "cleanup runs even though the operation failed");
    }
}
