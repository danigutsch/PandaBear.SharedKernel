using SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>Verifies bounded, metadata-only delivery inspection over real relational mappings.</summary>
[Trait(TestTraitNames.CategoryName, IntegrationTestTraitValues.DatabaseIntegrationCategory)]
[Trait(TestTraitNames.CapabilityName, TestTraitValues.IntegrationEventTransport)]
public sealed class PostgreSqlMessagingDeliveryInspectionTests(PostgreSqlFixture fixture)
{
    private static readonly DateTimeOffset Cutoff = new(2026, 11, 22, 8, 0, 0, TimeSpan.Zero);
    private const string Consumer = "delivery-inspection-consumer";

    [Fact]
    public async Task Outbox_membership_limits_the_database_page_before_cursor_and_limit()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct);
        var correlationId = Guid.NewGuid();
        var firstMatch = await scenario.SeedOutbox(Cutoff, ct);
        var secondMatch = await scenario.SeedOutbox(Cutoff, ct);
        var published = await scenario.SeedOutbox(Cutoff.AddMinutes(1), ct,
            message => message.MarkPublished(Cutoff.AddMinutes(2)));
        var unrelated = await scenario.SeedOutbox(Cutoff.AddMinutes(2), ct);
        await scenario.SeedCorrelation(correlationId, [firstMatch.EventId, secondMatch.EventId, published.EventId], ct);
        var filter = new IntegrationEventDeliveryFilter(null, firstMatch.EventType, Cutoff);

        // Act
        var first = await scenario.InspectOutbox(filter, 1, null, ct, correlationId);
        var firstCommand = scenario.ReadCommands.ShouldHaveSingleItem();
        var second = await scenario.InspectOutbox(filter, 1, first.NextCursor, ct, correlationId);
        var secondCommand = scenario.ReadCommands.ShouldHaveSingleItem();
        var missing = await scenario.InspectOutbox(filter, 1, null, ct, Guid.NewGuid());
        var excluded = await scenario.InspectOutbox(filter with { EventId = unrelated.EventId }, 1, null, ct, correlationId);

        // Assert
        first.Entries.ShouldHaveSingleItem();
        first.NextCursor.ShouldNotBeNull();
        second.Entries.ShouldHaveSingleItem();
        second.NextCursor.ShouldBeNull();
        first.Entries.Concat(second.Entries).Select(entry => entry.StorageId)
            .ShouldBe(new[] { firstMatch.Id, secondMatch.Id }.OrderDescending());
        missing.Entries.ShouldBeEmpty();
        excluded.Entries.ShouldBeEmpty();
        firstCommand.ShouldContain("delivery_correlation", StringComparison.Ordinal);
        firstCommand.ShouldContain("LIMIT", StringComparison.OrdinalIgnoreCase);
        firstCommand.ShouldNotContain("Payload", StringComparison.Ordinal);
        firstCommand.ShouldNotContain("ExtensionAttributesJson", StringComparison.Ordinal);
        secondCommand.ShouldNotContain("OFFSET", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transport_membership_stays_scoped_to_the_consumer_and_unprocessed_rows()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct);
        var correlationId = Guid.NewGuid();
        var failedEvent = Guid.NewGuid().ToString("D");
        var processedEvent = Guid.NewGuid().ToString("D");
        await scenario.SeedTransport(Consumer, Cutoff, ct, row => row.MarkPoisoned(Cutoff, "private"), failedEvent);
        await scenario.SeedTransport("other-consumer", Cutoff.AddMinutes(3), ct, eventId: failedEvent);
        await scenario.SeedTransport(Consumer, Cutoff.AddMinutes(2), ct,
            row => row.MarkProcessed(Cutoff.AddMinutes(3)), processedEvent);
        await scenario.SeedTransport(Consumer, Cutoff.AddMinutes(1), ct);
        await scenario.SeedCorrelation(correlationId, [failedEvent, processedEvent], ct);

        // Act
        var page = await scenario.InspectTransport(Consumer, default, 1, null, ct, correlationId);
        var command = scenario.ReadCommands.ShouldHaveSingleItem();

        // Assert
        var entry = page.Entries.ShouldHaveSingleItem();
        entry.EventId.ShouldBe(failedEvent);
        entry.TerminalAt.ShouldBe(Cutoff);
        page.NextCursor.ShouldBeNull();
        command.ShouldContain("delivery_correlation", StringComparison.Ordinal);
        command.ShouldContain("ConsumerName", StringComparison.Ordinal);
        command.ShouldNotContain("Payload", StringComparison.Ordinal);
        command.ShouldNotContain("LastConsumeError", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Outbox_inspection_pages_ties_and_filters_by_event_type_and_recorded_time()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct);
        await scenario.SeedOutbox(Cutoff.AddMinutes(-1), ct);
        await scenario.SeedOutbox(Cutoff.AddMinutes(1), ct, message => message.MarkPublished(Cutoff.AddMinutes(2)));
        var ids = new List<Guid>();
        string? eventType = null;
        for (var sequence = 0; sequence < 3; sequence++)
        {
            await scenario.SeedOutbox(Cutoff, ct, message =>
            {
                eventType = message.EventType;
                ids.Add(message.Id);
                message.MarkPublishFailed(Cutoff, Cutoff.AddHours(1), "synthetic-private-error");
            });
        }

        // Act
        var filter = new IntegrationEventDeliveryFilter(null, eventType, Cutoff.ToOffset(TimeSpan.FromHours(2)));
        var first = await scenario.InspectOutbox(filter, 2, null, ct);
        var firstCommand = scenario.ReadCommands.ShouldHaveSingleItem();
        var second = await scenario.InspectOutbox(filter, 2, first.NextCursor, ct);
        var secondCommand = scenario.ReadCommands.ShouldHaveSingleItem();
        var missingType = await scenario.InspectOutbox(filter with { EventType = "different-type" }, 2, null, ct);

        // Assert
        first.Entries.Count.ShouldBe(2);
        first.NextCursor.ShouldNotBeNull();
        second.Entries.ShouldHaveSingleItem().Attempts.ShouldBe(1);
        second.NextCursor.ShouldBeNull();
        missingType.Entries.ShouldBeEmpty();
        first.Entries.Concat(second.Entries).Select(entry => entry.StorageId).ShouldBe(ids.OrderDescending());
        firstCommand.ShouldContain("LIMIT", StringComparison.OrdinalIgnoreCase);
        firstCommand.ShouldNotContain("Payload", StringComparison.Ordinal);
        firstCommand.ShouldNotContain("LastPublishError", StringComparison.Ordinal);
        firstCommand.ShouldNotContain("ExtensionAttributesJson", StringComparison.Ordinal);
        secondCommand.ShouldContain("LIMIT", StringComparison.OrdinalIgnoreCase);
        secondCommand.ShouldNotContain("OFFSET", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Retained_outbox_lookup_observes_the_same_event_through_pending_retry_and_publication()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct);
        var message = await scenario.SeedOutbox(Cutoff, ct);
        var failedAt = Cutoff.AddMinutes(1);
        var retryAt = Cutoff.AddMinutes(2);
        var publishedAt = Cutoff.AddMinutes(3);

        // Act
        var pending = await scenario.InspectRetainedOutbox(message.EventId, ct);
        await scenario.TransitionOutbox(message.Id, row => row.MarkPublishFailed(failedAt, retryAt, "private-error"), ct);
        var retry = await scenario.InspectRetainedOutbox(message.EventId, ct);
        await scenario.TransitionOutbox(message.Id, row => row.MarkPublished(publishedAt), ct);
        var published = await scenario.InspectRetainedOutbox(message.EventId, ct);
        var command = scenario.ReadCommands.ShouldHaveSingleItem();
        var outstanding = await scenario.InspectOutbox(new(message.EventId, null, null), 1, null, ct);
        var absent = await scenario.InspectRetainedOutbox("different-event-id", ct);
        await scenario.DeleteOutbox(message.Id, ct);
        var purged = await scenario.InspectRetainedOutbox(message.EventId, ct);

        // Assert
        pending.ShouldNotBeNull().PublishedAt.ShouldBeNull();
        pending.FailedAttempts.ShouldBe(0);
        retry.ShouldNotBeNull().LastAttemptAt.ShouldBe(failedAt);
        retry.NextAttemptAt.ShouldBe(retryAt);
        retry.FailedAttempts.ShouldBe(1);
        published.ShouldNotBeNull().StorageId.ShouldBe(message.Id);
        published.EventId.ShouldBe(message.EventId);
        published.EventType.ShouldBe(message.EventType);
        published.RecordedAt.ShouldBe(Cutoff);
        published.PublishedAt.ShouldBe(publishedAt);
        published.LastAttemptAt.ShouldBe(publishedAt);
        published.FailedAttempts.ShouldBe(1);
        published.NextAttemptAt.ShouldBeNull();
        outstanding.Entries.ShouldBeEmpty();
        absent.ShouldBeNull();
        purged.ShouldBeNull();
        command.ShouldContain("health_outbox.operations_outbox", StringComparison.Ordinal);
        command.ShouldContain("LIMIT", StringComparison.OrdinalIgnoreCase);
        command.ShouldNotContain("Payload", StringComparison.Ordinal);
        command.ShouldNotContain("LastPublishError", StringComparison.Ordinal);
        command.ShouldNotContain("ExtensionAttributesJson", StringComparison.Ordinal);
        command.ShouldNotContain("Subject", StringComparison.Ordinal);
        command.ShouldNotContain("ClaimedBy", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Retained_outbox_lookup_works_with_metadata_column_grants()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct);
        var publishedAt = Cutoff.AddMinutes(1);
        var row = await scenario.SeedOutbox(Cutoff, ct, message => message.MarkPublished(publishedAt));

        // Act
        var result = await scenario.InspectRetainedOutboxWithMetadataGrants(row.EventId, ct);

        // Assert
        result.HasBroaderAccess.ShouldBeFalse();
        result.Delivery.ShouldNotBeNull().PublishedAt.ShouldBe(publishedAt);
        result.Delivery.StorageId.ShouldBe(row.Id);
    }

    [Fact]
    public async Task Retained_outbox_lookup_preserves_cancellation_and_reads_the_default_mapping()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct, useDefaultOutbox: true);
        var row = await scenario.SeedOutbox(Cutoff, ct);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> canceledRead = () => scenario.InspectRetainedOutbox(row.EventId, cancellation.Token);

        // Act
        var error = await canceledRead.ShouldThrowAssignableTo<OperationCanceledException>();
        var retained = await scenario.InspectRetainedOutbox(row.EventId, ct);

        // Assert
        error.CancellationToken.ShouldBe(cancellation.Token);
        retained.ShouldNotBeNull().StorageId.ShouldBe(row.Id);
        scenario.ReadCommands.ShouldHaveSingleItem().ShouldContain("messaging.outbox_messages", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Retained_outbox_lookup_rejects_a_missing_identity_without_SQL(string? eventId)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct);
        Func<Task> read = () => scenario.InspectRetainedOutbox(eventId!, ct);

        // Act
        var error = await read.ShouldThrowAssignableTo<ArgumentException>();

        // Assert
        error.ParamName.ShouldBe("eventId");
        scenario.ReadCommands.ShouldBeEmpty();
    }

    [Fact]
    public async Task Transport_inspection_reads_exact_event_and_consumer_with_terminal_metadata()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct);
        const string eventId = "synthetic-inspection-id";
        await scenario.SeedTransport("other-consumer", Cutoff, ct,
            message => message.MarkPoisoned(Cutoff, "other"), eventId);
        await scenario.SeedTransport(Consumer, Cutoff.AddMinutes(1), ct, message => message.MarkProcessed(Cutoff.AddMinutes(2)));
        await scenario.SeedTransport(Consumer, Cutoff, ct,
            message => message.MarkPoisoned(Cutoff, "synthetic-private-error"), eventId);
        await scenario.SeedTransport(Consumer, Cutoff.AddMinutes(-1), ct);

        // Act
        var page = await scenario.InspectTransport(Consumer, new(eventId, null, null), 10, null, ct);
        var command = scenario.ReadCommands.ShouldHaveSingleItem();

        // Assert
        var entry = page.Entries.ShouldHaveSingleItem();
        entry.EventId.ShouldBe(eventId);
        entry.TerminalAt.ShouldBe(Cutoff);
        entry.Attempts.ShouldBe(1);
        entry.NextAttemptAt.ShouldBeNull();
        page.NextCursor.ShouldBeNull();
        command.ShouldContain("ConsumerName", StringComparison.Ordinal);
        command.ShouldNotContain("Payload", StringComparison.Ordinal);
        command.ShouldNotContain("LastConsumeError", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Outbox_inspection_rejects_invalid_limits_without_a_database_read(int limit)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct);
        Func<Task> read = () => scenario.InspectOutbox(default, limit, null, ct);

        // Act
        var error = await read.ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        error.ParamName.ShouldBe("limit");
        scenario.ReadCommands.ShouldBeEmpty();
    }

    [Fact]
    public async Task Canceled_inspection_preserves_the_next_consumer_query()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> read = () => scenario.InspectTransport(Consumer, default, 1, null, cancellation.Token);

        // Act
        var error = await read.ShouldThrowAssignableTo<OperationCanceledException>();
        var next = await scenario.InspectTransport(Consumer, default, 1, null, ct);

        // Assert
        error.CancellationToken.ShouldBe(cancellation.Token);
        next.Entries.ShouldBeEmpty();
        next.NextCursor.ShouldBeNull();
    }
}
