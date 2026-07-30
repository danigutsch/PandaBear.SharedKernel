namespace SharedKernel.EventSourcing.Npgsql.Tests;

public sealed class PostgreSqlProjectionCheckpointStoreTests(PostgreSqlTestServerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture)
{
    [Fact]
    public async Task Save_upserts_projection_checkpoint()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlProjectionCheckpointStore(ConnectionString, options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var firstCheckpoint = new ProjectionCheckpoint("catalog-public-listing", 12);
        var secondCheckpoint = new ProjectionCheckpoint("catalog-public-listing", 27);

        // Act
        await store.Save(firstCheckpoint, TestContext.Current.CancellationToken);
        await store.Save(secondCheckpoint, TestContext.Current.CancellationToken);
        var savedCheckpoint = await store.GetCheckpoint("catalog-public-listing", TestContext.Current.CancellationToken);

        // Assert
        _ = savedCheckpoint.ShouldNotBeNull();
        savedCheckpoint.ProjectionName.ShouldBe("catalog-public-listing");
        savedCheckpoint.Position.ShouldBe(27);
    }

    [Fact]
    public async Task Save_does_not_move_projection_checkpoint_backward()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlProjectionCheckpointStore(ConnectionString, options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var currentCheckpoint = new ProjectionCheckpoint("catalog-public-listing", 27);
        var staleCheckpoint = new ProjectionCheckpoint("catalog-public-listing", 12);

        // Act
        await store.Save(currentCheckpoint, TestContext.Current.CancellationToken);
        await store.Save(staleCheckpoint, TestContext.Current.CancellationToken);
        var savedCheckpoint = await store.GetCheckpoint("catalog-public-listing", TestContext.Current.CancellationToken);

        // Assert
        _ = savedCheckpoint.ShouldNotBeNull();
        savedCheckpoint.Position.ShouldBe(27);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Save_rejects_missing_projection_name(string projectionName)
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlProjectionCheckpointStore(ConnectionString, options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var checkpoint = new ProjectionCheckpoint(projectionName, 12);

        // Act
        var exception = await ((Func<Task>)(() => store.Save(
            checkpoint,
            TestContext.Current.CancellationToken).AsTask())).ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("checkpoint.ProjectionName");
    }

    [Fact]
    public async Task Save_rejects_negative_position()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlProjectionCheckpointStore(ConnectionString, options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var checkpoint = new ProjectionCheckpoint("catalog-public-listing", -1);

        // Act
        var exception = await ((Func<Task>)(() => store.Save(
            checkpoint,
            TestContext.Current.CancellationToken).AsTask())).ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        exception.ParamName.ShouldBe("checkpoint.Position");
    }
}
