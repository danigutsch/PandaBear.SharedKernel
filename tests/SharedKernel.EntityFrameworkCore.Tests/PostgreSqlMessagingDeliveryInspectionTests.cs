using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Time.Testing;
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
    public async Task Workflow_correlation_survives_failed_publication_atomic_retry_and_retained_delivery()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(Cutoff);
        var workflowId = Guid.CreateVersion7();
        var probe = new RecordingEventEnvelopePublisher
        {
            Failure = new InvalidOperationException(new string('x', IntegrationEventOutboxMessage.LastPublishErrorMaxLength + 1)),
            FailOnce = true,
        };
        await using var scenario = await ContextQualifiedMessagingPostgreSqlScenario.Create(fixture, ct, clock, probe);
        var eventId = await scenario.EnqueueFirstOutbox(workflowId, ct);

        // Act
        var claimedForFailure = await scenario.PublishFirstOutbox(ct);
        var failedQueues = await scenario.InspectFirstDeliveryQueues(workflowId, ct);
        var failed = await scenario.InspectFirstRetainedOutbox(eventId, ct);
        var claimedBeforeDue = await scenario.PublishFirstOutbox(ct);
        clock.Advance(TimeSpan.FromSeconds(1));
        var claimedForRecovery = await scenario.PublishFirstOutbox(ct);
        var recoveredQueues = await scenario.InspectFirstDeliveryQueues(workflowId, ct);
        var recovered = await scenario.InspectFirstRetainedOutbox(eventId, ct);

        // Assert
        claimedForFailure.ShouldBe(1);
        claimedBeforeDue.ShouldBe(0);
        claimedForRecovery.ShouldBe(1);
        probe.Attempts.ShouldBe(2);
        var attempted = failedQueues.Outbox.Entries.ShouldHaveSingleItem();
        attempted.EventId.ShouldBe(eventId);
        attempted.WorkflowCorrelationId.ShouldBe(workflowId);
        attempted.Attempts.ShouldBe(1);
        attempted.LastAttemptAt.ShouldBe(Cutoff);
        attempted.NextAttemptAt.ShouldBe(Cutoff.AddSeconds(1));
        failedQueues.Transport.Entries.ShouldBeEmpty();
        failed.Retained.PublishedAt.ShouldBeNull();
        failed.StoredWorkflowId.ShouldBe(workflowId);
        failed.StoredErrorLength.ShouldBe(IntegrationEventOutboxMessage.LastPublishErrorMaxLength);
        recoveredQueues.Outbox.Entries.ShouldBeEmpty();
        var transported = recoveredQueues.Transport.Entries.ShouldHaveSingleItem();
        transported.EventId.ShouldBe(eventId);
        transported.WorkflowCorrelationId.ShouldBe(workflowId);
        recovered.Retained.EventId.ShouldBe(eventId);
        recovered.Retained.PublishedAt.ShouldBe(clock.GetUtcNow());
        recovered.Retained.FailedAttempts.ShouldBe(1);
        recovered.Retained.NextAttemptAt.ShouldBeNull();
        recovered.StoredWorkflowId.ShouldBe(workflowId);
        recovered.StoredErrorLength.ShouldBeNull();
    }

    [Fact]
    public async Task Workflow_correlation_expansion_keeps_legacy_rows_and_old_column_writes_compatible()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct, initializeLegacy: true);
        var workflowId = Guid.CreateVersion7();
        var historicalId = await scenario.SeedLegacyDelivery(Consumer, Cutoff, workflowId, ct);
        var filter = new IntegrationEventDeliveryFilter(null, null, Cutoff) { WorkflowCorrelationId = workflowId };

        // Act
        var migration = await scenario.ExpandWorkflowCorrelation(ct);
        var oldWriterId = await scenario.SeedLegacyDelivery(Consumer, Cutoff.AddMinutes(1), extensionWorkflowId: null, ct);
        var current = await scenario.SeedOutbox(Cutoff.AddMinutes(2), ct, workflowCorrelationId: workflowId);
        await scenario.SeedTransport(Consumer, Cutoff.AddMinutes(2), ct,
            eventId: current.EventId, extensionAttributesJson: current.ExtensionAttributesJson);
        var allOutbox = await scenario.InspectOutbox(default, 10, null, ct);
        var allTransport = await scenario.InspectTransport(Consumer, default, 10, null, ct);
        var correlatedOutbox = await scenario.InspectOutbox(filter, 10, null, ct);
        var correlatedTransport = await scenario.InspectTransport(Consumer, filter, 10, null, ct);

        // Assert
        migration.Count.ShouldBe(4);
        var columns = migration.OfType<AddColumnOperation>().ToArray();
        columns.Length.ShouldBe(2);
        columns.All(column => column.Name == "WorkflowCorrelationId" && column.ColumnType == "uuid"
            && column.IsNullable && column.DefaultValue == null && column.DefaultValueSql == null).ShouldBeTrue();
        var indexes = migration.OfType<CreateIndexOperation>().ToArray();
        indexes.ShouldHaveSingleItem(index => index.Table == "operations_outbox").Columns
            .ShouldBe(["WorkflowCorrelationId", "EnqueuedAt", "Id"]);
        indexes.ShouldHaveSingleItem(index => index.Table == "operations_transport").Columns
            .ShouldBe(["ConsumerName", "WorkflowCorrelationId", "ReceivedAt", "Id"]);
        allOutbox.Entries.Count.ShouldBe(3);
        allTransport.Entries.Count.ShouldBe(3);
        allOutbox.Entries.ShouldHaveSingleItem(row => row.EventId == historicalId).WorkflowCorrelationId.ShouldBeNull();
        allOutbox.Entries.ShouldHaveSingleItem(row => row.EventId == oldWriterId).WorkflowCorrelationId.ShouldBeNull();
        allTransport.Entries.ShouldHaveSingleItem(row => row.EventId == historicalId).WorkflowCorrelationId.ShouldBeNull();
        allTransport.Entries.ShouldHaveSingleItem(row => row.EventId == oldWriterId).WorkflowCorrelationId.ShouldBeNull();
        correlatedOutbox.Entries.ShouldHaveSingleItem().EventId.ShouldBe(current.EventId);
        correlatedTransport.Entries.ShouldHaveSingleItem().EventId.ShouldBe(current.EventId);
    }

    [Fact]
    public async Task Workflow_correlation_filters_durable_rows_before_pagination()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct);
        var workflowId = Guid.CreateVersion7();
        var older = await scenario.SeedOutbox(Cutoff, ct, workflowCorrelationId: workflowId);
        var newer = await scenario.SeedOutbox(Cutoff.AddMinutes(1), ct, workflowCorrelationId: workflowId);
        await scenario.SeedOutbox(Cutoff.AddMinutes(2), ct,
            row => row.MarkPublished(Cutoff.AddMinutes(3)), workflowId);
        await scenario.SeedOutbox(Cutoff.AddMinutes(4), ct);
        await scenario.SeedOutbox(Cutoff.AddMinutes(5), ct, workflowCorrelationId: Guid.CreateVersion7());
        await scenario.SeedTransport(Consumer, Cutoff, ct, eventId: older.EventId, extensionAttributesJson: older.ExtensionAttributesJson);
        await scenario.SeedTransport(Consumer, Cutoff.AddMinutes(1), ct,
            eventId: newer.EventId, extensionAttributesJson: newer.ExtensionAttributesJson);
        await scenario.SeedTransport("different-consumer", Cutoff.AddMinutes(2), ct,
            eventId: newer.EventId, extensionAttributesJson: newer.ExtensionAttributesJson);
        await scenario.SeedTransport(Consumer, Cutoff.AddMinutes(3), ct);
        var filter = new IntegrationEventDeliveryFilter(null, null, Cutoff) { WorkflowCorrelationId = workflowId };

        // Act
        var outboxFirst = await scenario.InspectOutbox(filter, 1, null, ct);
        var outboxCommand = scenario.ReadCommands.ShouldHaveSingleItem();
        var outboxSecond = await scenario.InspectOutbox(filter, 1, outboxFirst.NextCursor, ct);
        var transportFirst = await scenario.InspectTransport(Consumer, filter, 1, null, ct);
        var transportCommand = scenario.ReadCommands.ShouldHaveSingleItem();
        var transportSecond = await scenario.InspectTransport(Consumer, filter, 1, transportFirst.NextCursor, ct);

        // Assert
        var firstOutbox = outboxFirst.Entries.ShouldHaveSingleItem();
        firstOutbox.EventId.ShouldBe(newer.EventId);
        firstOutbox.WorkflowCorrelationId.ShouldBe(workflowId);
        outboxFirst.NextCursor.ShouldNotBeNull();
        outboxSecond.Entries.ShouldHaveSingleItem().EventId.ShouldBe(older.EventId);
        outboxSecond.NextCursor.ShouldBeNull();
        var firstTransport = transportFirst.Entries.ShouldHaveSingleItem();
        firstTransport.EventId.ShouldBe(newer.EventId);
        firstTransport.WorkflowCorrelationId.ShouldBe(workflowId);
        transportFirst.NextCursor.ShouldNotBeNull();
        transportSecond.Entries.ShouldHaveSingleItem().EventId.ShouldBe(older.EventId);
        transportSecond.NextCursor.ShouldBeNull();
        outboxCommand.ShouldContain("\"WorkflowCorrelationId\" =", StringComparison.Ordinal);
        outboxCommand.ShouldContain("LIMIT", StringComparison.OrdinalIgnoreCase);
        outboxCommand.ShouldNotContain("Payload", StringComparison.Ordinal);
        outboxCommand.ShouldNotContain("ExtensionAttributesJson", StringComparison.Ordinal);
        transportCommand.ShouldContain("\"WorkflowCorrelationId\" =", StringComparison.Ordinal);
        transportCommand.ShouldContain("ConsumerName", StringComparison.Ordinal);
        transportCommand.ShouldContain("LIMIT", StringComparison.OrdinalIgnoreCase);
        transportCommand.ShouldNotContain("Payload", StringComparison.Ordinal);
        transportCommand.ShouldNotContain("ExtensionAttributesJson", StringComparison.Ordinal);
    }

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
