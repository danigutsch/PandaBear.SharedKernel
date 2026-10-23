namespace PandaBear.EventSourcing.Npgsql.Tests;

public sealed class PostgreSqlControlledTimeTests(PostgreSqlTestServerFixture fixture) : PostgreSqlDatabaseTestBase(fixture)
{
    [Fact]
    public async Task Append_uses_one_controlled_instant_for_all_persisted_metadata()
    {
        // Arrange
        var first = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var clock = new AdvancingTimeProvider(first);
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        options.TimeProvider = clock;
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var id = StreamId.From("controlled-time");

        // Act
        var batch = await store.Append(id, ExpectedStreamRevision.NoStream,
            [new TestEvent("first"), new TestEvent("second")], TestContext.Current.CancellationToken);
        var readsAfterFirstAppend = clock.Reads;
        await using var initial = DataSource.CreateCommand($"SELECT created_at_utc, updated_at_utc FROM {options.Schema}.event_streams");
        bool hasRow;
        DateTimeOffset created;
        DateTimeOffset updated;
        await using (var reader = await initial.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            hasRow = await reader.ReadAsync(TestContext.Current.CancellationToken);
            created = await reader.GetFieldValueAsync<DateTimeOffset>(0, TestContext.Current.CancellationToken);
            updated = await reader.GetFieldValueAsync<DateTimeOffset>(1, TestContext.Current.CancellationToken);
        }

        var next = first.AddHours(1);
        clock.Instant = next.AddSeconds(-1);
        var appended = await store.Append(id, ExpectedStreamRevision.Any,
            [new TestEvent("third")], TestContext.Current.CancellationToken);
        var readsAfterSecondAppend = clock.Reads;
        await using var later = DataSource.CreateCommand($"SELECT created_at_utc, updated_at_utc FROM {options.Schema}.event_streams");
        await using var laterReader = await later.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var hasLaterRow = await laterReader.ReadAsync(TestContext.Current.CancellationToken);
        var laterCreated = await laterReader.GetFieldValueAsync<DateTimeOffset>(0, TestContext.Current.CancellationToken);
        var laterUpdated = await laterReader.GetFieldValueAsync<DateTimeOffset>(1, TestContext.Current.CancellationToken);

        // Assert
        batch.ShouldAllSatisfy(envelope => envelope.RecordedAt.ShouldBe(first));
        readsAfterFirstAppend.ShouldBe(1);
        hasRow.ShouldBeTrue();
        created.ShouldBe(first);
        updated.ShouldBe(first);
        appended.ShouldHaveSingleItem().RecordedAt.ShouldBe(next);
        readsAfterSecondAppend.ShouldBe(2);
        hasLaterRow.ShouldBeTrue();
        laterCreated.ShouldBe(first);
        laterUpdated.ShouldBe(next);
    }

    [Fact]
    public async Task Checkpoint_metadata_uses_the_configured_clock()
    {
        // Arrange
        var instant = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        options.TimeProvider = new AdvancingTimeProvider(instant);
        await using var store = new PostgreSqlProjectionCheckpointStore(ConnectionString, options);
        await store.Initialize(TestContext.Current.CancellationToken);

        // Act
        await store.Save(new ProjectionCheckpoint("projection", 1), TestContext.Current.CancellationToken);
        await using var command = DataSource.CreateCommand($"SELECT updated_at_utc FROM {options.Schema}.projection_checkpoints");
        var actual = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldBe(instant.UtcDateTime);
    }
}
