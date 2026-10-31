namespace PandaBear.Idempotency.Tests;

/// <summary>
/// Tests the default retention-owner overload of IIdempotencyStore.TryStart for stores that predate retention owners.
/// </summary>
public sealed class IIdempotencyStoreRetentionOwnerTests
{
    private static readonly IdempotencyOperation Operation = new(
        IdempotencyScope.From("projection:catalog-tour"),
        IdempotencyKey.From("event-42"));

    private static readonly DateTimeOffset StartedAt = new(2026, 10, 31, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TryStart_without_retention_owner_delegates_to_the_overload_without_owner()
    {
        // Arrange
        var store = new StartRecordingIdempotencyStore();
        IIdempotencyStore idempotencyStore = store;

        // Act
        var result = await idempotencyStore.TryStart(
            Operation,
            StartedAt,
            TimeSpan.FromMinutes(5),
            retentionOwner: null,
            CancellationToken.None);

        // Assert
        result.Started.ShouldBeTrue();
        store.StartCallCount.ShouldBe(1);
        store.StartedOperation.ShouldBe(Operation);
    }

    [Fact]
    public async Task TryStart_with_retention_owner_is_not_supported_by_a_store_without_the_overload()
    {
        // Arrange
        var store = new StartRecordingIdempotencyStore();
        IIdempotencyStore idempotencyStore = store;

        // Act, Assert
        await ShouldAssertionExtensions.ShouldThrow<NotSupportedException>((Func<Task>)(async () =>
        {
            await idempotencyStore.TryStart(
                Operation,
                StartedAt,
                TimeSpan.FromMinutes(5),
                "document-7",
                CancellationToken.None);
        }));
        store.StartCallCount.ShouldBe(0);
    }

    [Fact]
    public void Entry_without_retention_owner_keeps_the_positional_constructor_and_reports_no_owner()
    {
        // Act
        var entry = new IdempotencyEntry(Operation, IdempotencyEntryState.Started, StartedAt, null, null);

        var owned = new IdempotencyEntry(Operation, IdempotencyEntryState.Started, StartedAt, null, null)
        {
            RetentionOwner = "document-7",
        };

        // Assert
        entry.RetentionOwner.ShouldBeNull();
        owned.RetentionOwner.ShouldBe("document-7");
    }
}
