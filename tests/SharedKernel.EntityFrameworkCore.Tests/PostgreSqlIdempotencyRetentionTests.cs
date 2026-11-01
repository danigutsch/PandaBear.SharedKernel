using PandaBear.Idempotency;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// PostgreSQL tests of the idempotency retention owner and the retention purge.
/// </summary>
/// <param name="fixture">Shared PostgreSQL fixture that supplies the isolated database.</param>
[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.DatabaseIntegrationCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IdempotencyCapability)]
public sealed class PostgreSqlIdempotencyRetentionTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private const string TrackedScope = "documents.integration-event.signed.v1";
    private const string TrackedPrefix = "documents.";
    private const string UntrackedScope = "catalog.integration-event.tour.created.v1";

    private static readonly DateTimeOffset Old = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cutoff = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Recent = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private PostgreSqlIdempotencyStoreScenario? scenario;

    private PostgreSqlIdempotencyStoreScenario Scenario =>
        scenario ?? throw new InvalidOperationException("Test scenario is not initialized.");

    /// <summary>
    /// Creates the PostgreSQL idempotency scenario for the test.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        scenario = await PostgreSqlIdempotencyStoreScenario.Create(fixture, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Disposes the scenario created for the test, if any.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var currentScenario = scenario;
        scenario = null;

        if (currentScenario is not null)
        {
            await currentScenario.DisposeAsync();
        }
    }

    [Fact]
    public async Task Retention_owner_is_stored_in_the_database_and_returned()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var owned = await Scenario.Seed(TrackedScope, "document-7", Old, ct);
        var unowned = await Scenario.Seed(TrackedScope, null, Old, ct);

        // Act
        var ownedEntry = await Scenario.Find(owned, ct);
        var unownedEntry = await Scenario.Find(unowned, ct);

        // Assert
        ownedEntry.ShouldNotBeNull().RetentionOwner.ShouldBe("document-7");
        unownedEntry.ShouldNotBeNull().RetentionOwner.ShouldBeNull();
    }

    [Fact]
    public async Task PurgeExpired_does_not_delete_an_entry_whose_owner_is_held()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var held = await Scenario.Seed(TrackedScope, "document-7", Old, ct);
        var clear = await Scenario.Seed(TrackedScope, "document-8", Old, ct);

        // Act
        var result = await Scenario.Purge(Cutoff, TrackedPrefix, ["document-7"], 100, ct);

        // Assert
        result.ShouldBe(new IdempotencyRetentionPurgeResult(Purged: 1, Held: 1, Unattributed: 0));
        await Scenario.ShouldHaveRetained(held, ct);
        await Scenario.ShouldHaveDeleted(clear, ct);
    }

    [Fact]
    public async Task PurgeExpired_does_not_delete_a_held_entry_outside_the_owner_tracked_scopes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var held = await Scenario.Seed(UntrackedScope, "document-7", Old, ct);

        // Act
        var result = await Scenario.Purge(Cutoff, TrackedPrefix, ["document-7"], 100, ct);

        // Assert
        result.ShouldBe(new IdempotencyRetentionPurgeResult(Purged: 0, Held: 1, Unattributed: 0));
        await Scenario.ShouldHaveRetained(held, ct);
    }

    [Fact]
    public async Task PurgeExpired_retains_and_counts_an_unattributed_entry_in_an_owner_tracked_scope()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var unattributed = await Scenario.Seed(TrackedScope, null, Old, ct);

        // Act
        var result = await Scenario.Purge(Cutoff, TrackedPrefix, [], 100, ct);

        // Assert
        result.ShouldBe(new IdempotencyRetentionPurgeResult(Purged: 0, Held: 0, Unattributed: 1));
        await Scenario.ShouldHaveRetained(unattributed, ct);
    }

    [Fact]
    public async Task PurgeExpired_purges_an_unattributed_entry_outside_the_owner_tracked_scopes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var unattributed = await Scenario.Seed(UntrackedScope, null, Old, ct);

        // Act
        var result = await Scenario.Purge(Cutoff, TrackedPrefix, [], 100, ct);

        // Assert
        result.ShouldBe(new IdempotencyRetentionPurgeResult(Purged: 1, Held: 0, Unattributed: 0));
        await Scenario.ShouldHaveDeleted(unattributed, ct);
    }

    [Fact]
    public async Task PurgeExpired_without_owner_tracked_scopes_purges_every_unheld_entry()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var unattributed = await Scenario.Seed(TrackedScope, null, Old, ct);
        var owned = await Scenario.Seed(TrackedScope, "document-8", Old, ct);

        // Act
        var result = await Scenario.Purge(Cutoff, null, [], 100, ct);

        // Assert
        result.ShouldBe(new IdempotencyRetentionPurgeResult(Purged: 2, Held: 0, Unattributed: 0));
        await Scenario.ShouldHaveDeleted(unattributed, ct);
        await Scenario.ShouldHaveDeleted(owned, ct);
    }

    [Fact]
    public async Task PurgeExpired_keeps_entries_that_are_not_older_than_the_cutoff()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var recent = await Scenario.Seed(UntrackedScope, "document-8", Recent, ct);
        var atCutoff = await Scenario.Seed(UntrackedScope, "document-8", Cutoff, ct);

        // Act
        var result = await Scenario.Purge(Cutoff, TrackedPrefix, [], 100, ct);

        // Assert
        result.ShouldBe(new IdempotencyRetentionPurgeResult(Purged: 0, Held: 0, Unattributed: 0));
        await Scenario.ShouldHaveRetained(recent, ct);
        await Scenario.ShouldHaveRetained(atCutoff, ct);
    }

    [Fact]
    public async Task PurgeExpired_does_not_count_unexpired_held_or_unattributed_entries()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await Scenario.Seed(TrackedScope, "document-7", Recent, ct);
        await Scenario.Seed(TrackedScope, null, Recent, ct);

        // Act
        var result = await Scenario.Purge(Cutoff, TrackedPrefix, ["document-7"], 100, ct);

        // Assert
        result.ShouldBe(new IdempotencyRetentionPurgeResult(0, 0, 0));
    }

    [Fact]
    public async Task PurgeExpired_deletes_at_most_one_batch_oldest_first_and_reports_the_rest_on_the_next_batch()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var third = await Scenario.Seed(UntrackedScope, "document-8", Old.AddDays(3), ct);
        var first = await Scenario.Seed(UntrackedScope, "document-8", Old.AddDays(1), ct);
        var second = await Scenario.Seed(UntrackedScope, "document-8", Old.AddDays(2), ct);

        // Act
        var firstBatch = await Scenario.Purge(Cutoff, null, [], 2, ct);

        // Assert
        firstBatch.Purged.ShouldBe(2);
        await Scenario.ShouldHaveDeleted(first, ct);
        await Scenario.ShouldHaveDeleted(second, ct);
        await Scenario.ShouldHaveRetained(third, ct);

        // Act
        var secondBatch = await Scenario.Purge(Cutoff, null, [], 2, ct);

        // Assert
        secondBatch.Purged.ShouldBe(1);
        await Scenario.ShouldHaveEntryCount(0, ct);
    }

    [Fact]
    public async Task PurgeExpired_batch_of_exactly_the_purgeable_count_purges_all_of_them()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await Scenario.Seed(UntrackedScope, "document-8", Old, ct);
        await Scenario.Seed(UntrackedScope, "document-9", Old, ct);

        // Act
        var result = await Scenario.Purge(Cutoff, null, [], 2, ct);

        // Assert
        result.Purged.ShouldBe(2);
        await Scenario.ShouldHaveEntryCount(0, ct);
    }

    [Fact]
    public async Task PurgeExpired_retained_entries_do_not_use_up_the_batch()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await Scenario.Seed(TrackedScope, "document-7", Old, ct);
        await Scenario.Seed(TrackedScope, null, Old.AddDays(1), ct);
        var purgeable = await Scenario.Seed(TrackedScope, "document-8", Old.AddDays(2), ct);

        // Act
        var result = await Scenario.Purge(Cutoff, TrackedPrefix, ["document-7"], 1, ct);

        // Assert
        result.ShouldBe(new IdempotencyRetentionPurgeResult(Purged: 1, Held: 1, Unattributed: 1));
        await Scenario.ShouldHaveDeleted(purgeable, ct);
        await Scenario.ShouldHaveEntryCount(2, ct);
    }

    [Fact]
    public async Task PurgeExpired_uses_the_completion_time_of_a_completed_entry()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var operation = await Scenario.Seed(UntrackedScope, "document-8", Old, ct);
        await Scenario.Complete(operation, Recent, ct);

        // Act
        var result = await Scenario.Purge(Cutoff, null, [], 100, ct);

        // Assert
        result.Purged.ShouldBe(0);
        await Scenario.ShouldHaveRetained(operation, ct);
    }

    [Fact]
    public async Task PurgeExpired_treats_the_scope_prefix_literally()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var other = await Scenario.Seed("documentsXsigned", null, Old, ct);

        // Act
        var result = await Scenario.Purge(Cutoff, "documents_", [], 100, ct);

        // Assert
        result.ShouldBe(new IdempotencyRetentionPurgeResult(Purged: 1, Held: 0, Unattributed: 0));
        await Scenario.ShouldHaveDeleted(other, ct);
    }

    [Fact]
    public async Task PurgeExpired_batches_started_at_the_same_time_never_delete_the_same_entry_twice()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        for (var index = 0; index < 20; index++)
        {
            await Scenario.Seed(UntrackedScope, "document-8", Old, ct);
        }

        // Act
        var batches = await Task.WhenAll(
            Scenario.Purge(Cutoff, null, [], 20, ct).AsTask(),
            Scenario.Purge(Cutoff, null, [], 20, ct).AsTask());

        // Assert
        batches.Sum(static batch => batch.Purged).ShouldBe(20);
        await Scenario.ShouldHaveEntryCount(0, ct);
    }

    [Fact]
    public async Task PurgeExpired_restricted_to_a_scope_prefix_leaves_expired_entries_of_other_scopes_untouched()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var inScope = await Scenario.Seed(TrackedScope, "document-8", Old, ct);
        var inScopeUnowned = await Scenario.Seed(TrackedScope, null, Old, ct);
        var otherOwned = await Scenario.Seed(UntrackedScope, "document-8", Old, ct);
        var otherUnowned = await Scenario.Seed(UntrackedScope, null, Old, ct);
        var otherHeld = await Scenario.Seed(UntrackedScope, "document-7", Old, ct);

        // Act
        var result = await Scenario.Purge(Cutoff, TrackedPrefix, ["document-7"], 100, ct, TrackedPrefix);

        // Assert
        result.ShouldBe(new IdempotencyRetentionPurgeResult(Purged: 1, Held: 0, Unattributed: 1));
        await Scenario.ShouldHaveDeleted(inScope, ct);
        await Scenario.ShouldHaveRetained(inScopeUnowned, ct);
        await Scenario.ShouldHaveRetained(otherOwned, ct);
        await Scenario.ShouldHaveRetained(otherUnowned, ct);
        await Scenario.ShouldHaveRetained(otherHeld, ct);
    }

    [Fact]
    public async Task PurgeExpired_restricted_to_a_scope_prefix_counts_a_held_entry_in_scope_only()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var held = await Scenario.Seed(TrackedScope, "document-7", Old, ct);
        await Scenario.Seed(UntrackedScope, "document-7", Old, ct);

        // Act
        var result = await Scenario.Purge(Cutoff, null, ["document-7"], 100, ct, TrackedPrefix);

        // Assert
        result.ShouldBe(new IdempotencyRetentionPurgeResult(Purged: 0, Held: 1, Unattributed: 0));
        await Scenario.ShouldHaveRetained(held, ct);
    }

    [Fact]
    public async Task PurgeExpired_restricted_to_a_scope_prefix_fills_the_batch_from_that_scope_only()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var other = await Scenario.Seed(UntrackedScope, "document-8", Old, ct);
        var first = await Scenario.Seed(TrackedScope, "document-8", Old.AddDays(1), ct);
        var second = await Scenario.Seed(TrackedScope, "document-8", Old.AddDays(2), ct);
        var third = await Scenario.Seed(TrackedScope, "document-8", Old.AddDays(3), ct);

        // Act
        var firstBatch = await Scenario.Purge(Cutoff, null, [], 2, ct, TrackedPrefix);
        var secondBatch = await Scenario.Purge(Cutoff, null, [], 2, ct, TrackedPrefix);

        // Assert
        firstBatch.Purged.ShouldBe(2);
        secondBatch.Purged.ShouldBe(1);
        await Scenario.ShouldHaveDeleted(first, ct);
        await Scenario.ShouldHaveDeleted(second, ct);
        await Scenario.ShouldHaveDeleted(third, ct);
        await Scenario.ShouldHaveRetained(other, ct);
    }
}
