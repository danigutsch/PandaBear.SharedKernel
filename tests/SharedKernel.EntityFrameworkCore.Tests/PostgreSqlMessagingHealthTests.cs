namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>Verifies messaging health aggregates against context-specific PostgreSQL mappings.</summary>
[Trait(TestTraitNames.CategoryName, IntegrationTestTraitValues.DatabaseIntegrationCategory)]
[Trait(TestTraitNames.CapabilityName, TestTraitValues.IntegrationEventTransport)]
public sealed class PostgreSqlMessagingHealthTests(PostgreSqlFixture fixture)
{
    private static readonly DateTimeOffset Cutoff = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);
    private const string Consumer = "health-consumer";

    [Fact]
    public async Task Empty_messaging_tables_return_zero_counts_and_absent_timestamps()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct);

        // Act
        var outbox = await scenario.ReadOutbox(Cutoff, ct);
        var transport = await scenario.ReadTransport(Consumer, Cutoff, ct);

        // Assert
        outbox.PendingCount.ShouldBe(0L);
        outbox.OldestEnqueuedAt.ShouldBeNull();
        outbox.RetryCount.ShouldBe(0L);
        outbox.OldestRetryEnqueuedAt.ShouldBeNull();
        outbox.DueRetryCount.ShouldBe(0L);
        transport.TerminalCount.ShouldBe(0L);
        transport.OldestPoisonedAt.ShouldBeNull();
        transport.RetryCount.ShouldBe(0L);
        transport.OldestRetryReceivedAt.ShouldBeNull();
        transport.DueRetryCount.ShouldBe(0L);
    }

    [Fact]
    public async Task Outbox_counts_unpublished_messages_and_distinguishes_due_retries()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct);
        await scenario.SeedOutbox(Cutoff.AddDays(-3), ct, message =>
        {
            message.MarkPublishFailed(Cutoff.AddDays(-2), Cutoff.AddDays(-1), "synthetic-error");
            message.MarkPublished(Cutoff.AddDays(-1));
        });
        await scenario.SeedOutbox(Cutoff.AddHours(-5), ct);
        await scenario.SeedOutbox(Cutoff.AddHours(-4), ct,
            message => message.MarkPublishFailed(Cutoff.AddHours(-2), Cutoff.AddHours(1), "synthetic-error"));
        await scenario.SeedOutbox(Cutoff.AddHours(-3), ct, message =>
        {
            message.MarkPublishFailed(Cutoff.AddHours(-1), Cutoff, "synthetic-error");
            message.MarkClaimed("test-relay", Cutoff.AddMinutes(2));
        });

        // Act
        var health = await scenario.ReadOutbox(Cutoff.ToOffset(TimeSpan.FromHours(2)), ct);

        // Assert
        health.PendingCount.ShouldBe(3L);
        health.OldestEnqueuedAt.ShouldBe(Cutoff.AddHours(-5));
        health.RetryCount.ShouldBe(2L);
        health.OldestRetryEnqueuedAt.ShouldBe(Cutoff.AddHours(-4));
        health.DueRetryCount.ShouldBe(1L);
    }

    [Fact]
    public async Task Reads_propagate_cancellation_and_leave_the_next_query_available()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> readOutbox = () => scenario.ReadOutbox(Cutoff, cancellation.Token);
        Func<Task> readTransport = () => scenario.ReadTransport(Consumer, Cutoff, cancellation.Token);

        // Act
        var outboxCancellation = await readOutbox.ShouldThrowAssignableTo<OperationCanceledException>();
        var transportCancellation = await readTransport.ShouldThrowAssignableTo<OperationCanceledException>();
        var outbox = await scenario.ReadOutbox(Cutoff, ct);

        // Assert
        outboxCancellation.CancellationToken.ShouldBe(cancellation.Token);
        transportCancellation.CancellationToken.ShouldBe(cancellation.Token);
        outbox.PendingCount.ShouldBe(0L);
    }

    [Fact]
    public async Task Each_health_read_selects_only_aggregate_metadata_in_one_statement()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct);
        await scenario.SeedOutbox(Cutoff.AddHours(-2), ct,
            message => message.MarkPublishFailed(Cutoff.AddHours(-1), Cutoff, "synthetic-private-error"));
        await scenario.SeedTransport(Consumer, Cutoff.AddHours(-2), ct,
            message => message.MarkPoisoned(Cutoff.AddHours(-1), "synthetic-private-error"));

        // Act
        await scenario.ReadOutbox(Cutoff, ct);
        var outboxCommands = scenario.ReadCommands.ToArray();
        await scenario.ReadTransport(Consumer, Cutoff, ct);
        var transportCommands = scenario.ReadCommands;

        // Assert
        var outbox = outboxCommands.ShouldHaveSingleItem();
        var transport = transportCommands.ShouldHaveSingleItem();
        outbox.ShouldContain("count(", StringComparison.OrdinalIgnoreCase);
        outbox.ShouldContain("min(", StringComparison.OrdinalIgnoreCase);
        outbox.ShouldNotContain("Payload", StringComparison.Ordinal);
        outbox.ShouldNotContain("LastPublishError", StringComparison.Ordinal);
        transport.ShouldContain("count(", StringComparison.OrdinalIgnoreCase);
        transport.ShouldContain("min(", StringComparison.OrdinalIgnoreCase);
        transport.ShouldNotContain("Payload", StringComparison.Ordinal);
        transport.ShouldNotContain("LastConsumeError", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Transport_scopes_retry_and_terminal_health_to_the_selected_consumer()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scenario = await PostgreSqlMessagingHealthScenario.Create(fixture, ct);
        await scenario.SeedTransport("other-consumer", Cutoff.AddDays(-4), ct,
            message => message.MarkPoisoned(Cutoff.AddDays(-3), "synthetic-error"));
        await scenario.SeedTransport("other-consumer", Cutoff.AddDays(-3), ct,
            message => message.MarkConsumeFailed(Cutoff.AddDays(-2), Cutoff.AddDays(-1), "synthetic-error"));
        await scenario.SeedTransport(Consumer, Cutoff.AddDays(-2), ct, message =>
        {
            message.MarkConsumeFailed(Cutoff.AddDays(-1), Cutoff.AddHours(-6), "synthetic-error");
            message.MarkProcessed(Cutoff.AddHours(-6));
        });
        await scenario.SeedTransport(Consumer, Cutoff.AddHours(-8), ct, message =>
        {
            message.MarkPoisoned(Cutoff.AddHours(-7), "synthetic-error");
            message.ReleaseFromPoison(Cutoff.AddHours(-6));
        });
        await scenario.SeedTransport(Consumer, Cutoff.AddHours(-7), ct,
            message => message.MarkPoisoned(Cutoff.AddHours(-3), "synthetic-error"));
        await scenario.SeedTransport(Consumer, Cutoff.AddHours(-4), ct,
            message => message.MarkPoisoned(Cutoff.AddHours(-2), "synthetic-error"));
        await scenario.SeedTransport(Consumer, Cutoff.AddHours(-6), ct,
            message => message.MarkConsumeFailed(Cutoff.AddHours(-2), Cutoff.AddHours(1), "synthetic-error"));
        await scenario.SeedTransport(Consumer, Cutoff.AddHours(-5), ct,
            message => message.MarkConsumeFailed(Cutoff.AddHours(-1), Cutoff, "synthetic-error"));

        // Act
        var health = await scenario.ReadTransport(Consumer, Cutoff, ct);

        // Assert
        health.TerminalCount.ShouldBe(2L);
        health.OldestPoisonedAt.ShouldBe(Cutoff.AddHours(-3));
        health.RetryCount.ShouldBe(2L);
        health.OldestRetryReceivedAt.ShouldBe(Cutoff.AddHours(-6));
        health.DueRetryCount.ShouldBe(1L);
    }
}
