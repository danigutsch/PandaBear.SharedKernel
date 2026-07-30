namespace SharedKernel.EventSourcing.Npgsql.Tests;

public sealed class PostgreSqlTestCleanupTests
{
    [Fact]
    public async Task Operation_and_cleanup_failures_are_preserved()
    {
        // Arrange
        var operationFailure = new InvalidOperationException("startup failed");
        var cleanupFailure = new IOException("cleanup failed");
        var cleanupOrder = new List<string>();
        var cleanup = new TrackingCleanupAction("app", cleanupOrder, cleanupFailure);

        // Act
        Func<Task> disposeResources = () => PostgreSqlTestCleanup.Run(
            operationFailure,
            cleanup.Invoke);
        var exception = await disposeResources.ShouldThrow<AggregateException>();

        // Assert
        cleanupOrder.ShouldBe(["app"]);
        exception.InnerExceptions.Count.ShouldBe(2);
        exception.InnerExceptions[0].ShouldBeSameAs(operationFailure);
        exception.InnerExceptions[1].ShouldBeSameAs(cleanupFailure);
    }

    [Fact]
    public async Task Every_cleanup_runs_and_multiple_failures_are_preserved()
    {
        // Arrange
        var firstFailure = new IOException("data source failed");
        var secondFailure = new InvalidOperationException("database drop failed");
        var cleanupOrder = new List<string>();
        var first = new TrackingCleanupAction("data-source", cleanupOrder, firstFailure);
        var second = new TrackingCleanupAction("database", cleanupOrder, secondFailure);

        // Act
        Func<Task> disposeResources = () => PostgreSqlTestCleanup.Run(
            operationFailure: null,
            first.Invoke,
            second.Invoke);
        var exception = await disposeResources.ShouldThrow<AggregateException>();

        // Assert
        cleanupOrder.ShouldBe(["data-source", "database"]);
        exception.InnerExceptions.Count.ShouldBe(2);
        exception.InnerExceptions[0].ShouldBeSameAs(firstFailure);
        exception.InnerExceptions[1].ShouldBeSameAs(secondFailure);
    }
}
