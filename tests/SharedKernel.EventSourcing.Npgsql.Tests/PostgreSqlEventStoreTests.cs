namespace SharedKernel.EventSourcing.Npgsql.Tests;

public sealed class PostgreSqlEventStoreTests(PostgreSqlTestServerFixture fixture)
    : PostgreSqlDatabaseTestBase(fixture)
{
    [Fact]
    public async Task Append_with_no_stream_loads_persisted_events_in_revision_order()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var streamId = StreamId.From("catalog-tour-test-1");
        var events = new TestEvent[]
        {
            new("draft-created"),
            new("published"),
        };

        // Act
        var appended = await store.Append(streamId, ExpectedStreamRevision.NoStream, events, TestContext.Current.CancellationToken);
        var envelopes = await store.Load(streamId, afterRevision: null, TestContext.Current.CancellationToken);
        var persistedEnvelopes = appended.ToArray();
        var loadedEnvelopes = envelopes.ToArray();

        // Assert
        persistedEnvelopes.Length.ShouldBe(2);
        persistedEnvelopes[0].EventId.ShouldBe(loadedEnvelopes[0].EventId);
        persistedEnvelopes[0].Position.ShouldBe(loadedEnvelopes[0].Position);
        persistedEnvelopes[1].EventId.ShouldBe(loadedEnvelopes[1].EventId);
        persistedEnvelopes[1].Position.ShouldBe(loadedEnvelopes[1].Position);
        (envelopes).ShouldMatchCollection(first =>
            {
                (first.Revision.Value).ShouldBe(1);
                (first.EventType).ShouldBe(TestEventSerializer.EventType);
                var eventData = (first.Data).ShouldBeOfType<TestEvent>();
                (eventData.Name).ShouldBe("draft-created");
            }, second =>
            {
                (second.Revision.Value).ShouldBe(2);
                (second.EventType).ShouldBe(TestEventSerializer.EventType);
                var eventData = (second.Data).ShouldBeOfType<TestEvent>();
                (eventData.Name).ShouldBe("published");
            });
    }

    [Fact]
    public async Task Append_returns_the_recorded_at_persisted_for_subsequent_load()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var streamId = StreamId.From("catalog-tour-recorded-at");

        // Act
        var appended = await store.Append(
            streamId,
            ExpectedStreamRevision.NoStream,
            [new TestEvent("draft-created")],
            TestContext.Current.CancellationToken);
        var loaded = await store.Load(streamId, afterRevision: null, TestContext.Current.CancellationToken);

        // Assert
        appended.ShouldHaveSingleItem().RecordedAt.ShouldBe(loaded.ShouldHaveSingleItem().RecordedAt);
    }

    [Fact]
    public async Task Append_with_stale_expected_revision_reports_conflict()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var streamId = StreamId.From("catalog-tour-test-2");
        await store.Append(
            streamId,
            ExpectedStreamRevision.NoStream,
            [new TestEvent("draft-created")],
            TestContext.Current.CancellationToken);

        // Act
        var exception = await ((Func<Task>)(() => store.Append(
                streamId,
                ExpectedStreamRevision.NoStream,
                [new TestEvent("published")],
                TestContext.Current.CancellationToken).AsTask())).ShouldThrow<ExpectedStreamRevisionConflictException>();

        // Assert
        (exception.StreamId).ShouldBe(streamId);
        (exception.ExpectedRevision.RequiresEmptyStream).ShouldBeTrue();
        var actualRevision = (exception.ActualRevision).ShouldBeOfType<StreamRevision>();
        (actualRevision.Value).ShouldBe(1);
    }

    [Fact]
    public async Task Append_with_concurrent_no_stream_writers_reports_conflicts()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var streamId = StreamId.From("catalog-tour-concurrent");

        // Act
        var appendTasks = Enumerable.Range(1, 10)
            .Select(index => PostgreSqlEventStoreTestsHelpers.CaptureAppendResult(store, streamId, new TestEvent($"event-{index}")))
            .ToArray();
        var results = await Task.WhenAll(appendTasks);

        // Assert
        (results.Count(result => result is null)).ShouldBe(1);
        (results.OfType<ExpectedStreamRevisionConflictException>().Count()).ShouldBe(9);
        var envelopes = await store.Load(streamId, afterRevision: null, TestContext.Current.CancellationToken);
        var envelope = (envelopes).ShouldHaveSingleItem();
        (envelope.Position).ShouldBe(1);
        (envelope.Revision.Value).ShouldBe(1);
    }

    [Fact]
    public async Task Append_with_concurrent_any_writers_appends_all_events()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var streamId = StreamId.From("catalog-tour-any-concurrent");

        // Act
        var appendTasks = Enumerable.Range(1, 10)
            .Select(index => store.Append(
                streamId,
                ExpectedStreamRevision.Any,
                [new TestEvent($"event-{index}")],
                TestContext.Current.CancellationToken).AsTask())
            .ToArray();
        await Task.WhenAll(appendTasks);

        // Assert
        var envelopes = await store.Load(streamId, afterRevision: null, TestContext.Current.CancellationToken);
        (envelopes.Count).ShouldBe(10);
        (envelopes.Select(envelope => (int)envelope.Revision.Value)).ShouldBe(Enumerable.Range(1, 10));
    }

    [Fact]
    public async Task Append_with_specific_revision_after_empty_stream_reports_conflict()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var streamId = StreamId.From("catalog-tour-empty-conflict");

        // Act
        var exception = await ((Func<Task>)(() => store.Append(
                streamId,
                ExpectedStreamRevision.From(StreamRevision.From(1)),
                [new TestEvent("published")],
                TestContext.Current.CancellationToken).AsTask())).ShouldThrow<ExpectedStreamRevisionConflictException>();

        // Assert
        (exception.ActualRevision).ShouldBeNull();
    }

    [Fact]
    public async Task Load_after_loads_events_in_global_position_order()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var firstStreamId = StreamId.From("catalog-tour-global-1");
        var secondStreamId = StreamId.From("catalog-tour-global-2");
        await store.Append(
            firstStreamId,
            ExpectedStreamRevision.NoStream,
            [new TestEvent("first")],
            TestContext.Current.CancellationToken);
        await store.Append(
            secondStreamId,
            ExpectedStreamRevision.NoStream,
            [new TestEvent("second")],
            TestContext.Current.CancellationToken);
        var firstEnvelope = (await store.Load(firstStreamId, afterRevision: null, TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

        // Act
        var envelopes = await store.LoadAfter(firstEnvelope.Position, maxCount: 10, TestContext.Current.CancellationToken);

        // Assert
        var envelope = (envelopes).ShouldHaveSingleItem();
        (envelope.StreamId).ShouldBe(secondStreamId);
        (envelope.Position > firstEnvelope.Position).ShouldBeTrue();
        var eventData = (envelope.Data).ShouldBeOfType<TestEvent>();
        (eventData.Name).ShouldBe("second");
    }

    [Fact]
    public async Task Load_after_can_checkpoint_concurrent_cross_stream_appends()
    {
        // Arrange
        var options = PostgreSqlEventStoreTestsHelpers.CreateOptions();
        await using var store = new PostgreSqlEventStore(ConnectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);

        // Act
        var appendTasks = Enumerable.Range(1, 10)
            .Select(index => store.Append(
                StreamId.From($"catalog-tour-global-concurrent-{index}"),
                ExpectedStreamRevision.NoStream,
                [new TestEvent($"event-{index}")],
                TestContext.Current.CancellationToken).AsTask())
            .ToArray();
        await Task.WhenAll(appendTasks);
        var firstBatch = await store.LoadAfter(position: 0, maxCount: 5, TestContext.Current.CancellationToken);
        var checkpoint = firstBatch.Max(envelope => envelope.Position);
        var secondBatch = await store.LoadAfter(checkpoint, maxCount: 10, TestContext.Current.CancellationToken);

        // Assert
        (firstBatch.Count).ShouldBe(5);
        (secondBatch.Count).ShouldBe(5);
        (firstBatch.Concat(secondBatch).Select(envelope => envelope.EventId).Distinct().Count()).ShouldBe(10);
    }

}
