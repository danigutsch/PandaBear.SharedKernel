namespace SharedKernel.Idempotency.Tests;

public sealed class IIdempotencyStoreTests
{
    [Fact]
    public async Task Complete_without_result_fingerprint_forwards_null_fingerprint()
    {
        // Arrange
        var store = new RecordingIdempotencyStore();
        IIdempotencyStore idempotencyStore = store;
        var operation = new IdempotencyOperation(
            IdempotencyScope.From("request:create-booking"),
            IdempotencyKey.From("request-123"));
        var completedAt = new DateTimeOffset(2026, 6, 21, 10, 0, 1, TimeSpan.Zero);
        using var cts = new CancellationTokenSource();

        // Act
        await idempotencyStore.Complete(operation, completedAt, cts.Token);

        // Assert
        TestAssert.Equal(operation, store.CompletedOperation);
        TestAssert.Equal(completedAt, store.CompletedAt);
        TestAssert.Null(store.ResultFingerprint);
        TestAssert.Equal(cts.Token, store.CancellationToken);
        TestAssert.Equal(1, store.CompleteCallCount);
    }

    [Fact]
    public async Task Complete_with_result_fingerprint_forwards_fingerprint()
    {
        // Arrange
        var store = new RecordingIdempotencyStore();
        var operation = new IdempotencyOperation(
            IdempotencyScope.From("request:create-booking"),
            IdempotencyKey.From("request-123"));
        var completedAt = new DateTimeOffset(2026, 6, 21, 10, 0, 1, TimeSpan.Zero);
        using var cts = new CancellationTokenSource();

        // Act
        await store.Complete(operation, completedAt, "sha256:booking-response", cts.Token);

        // Assert
        TestAssert.Equal(operation, store.CompletedOperation);
        TestAssert.Equal(completedAt, store.CompletedAt);
        TestAssert.Equal("sha256:booking-response", store.ResultFingerprint);
        TestAssert.Equal(cts.Token, store.CancellationToken);
        TestAssert.Equal(1, store.CompleteCallCount);
    }

    [Fact]
    public async Task Complete_with_null_result_fingerprint_preserves_null_fingerprint()
    {
        // Arrange
        var store = new RecordingIdempotencyStore();
        var operation = new IdempotencyOperation(
            IdempotencyScope.From("projection:catalog-tour"),
            IdempotencyKey.From("event-42"));
        var completedAt = new DateTimeOffset(2026, 6, 21, 10, 0, 2, TimeSpan.Zero);

        // Act
        await store.Complete(operation, completedAt, resultFingerprint: null, CancellationToken.None);

        // Assert
        TestAssert.Equal(operation, store.CompletedOperation);
        TestAssert.Equal(completedAt, store.CompletedAt);
        TestAssert.Null(store.ResultFingerprint);
        TestAssert.Equal(CancellationToken.None, store.CancellationToken);
        TestAssert.Equal(1, store.CompleteCallCount);
    }

    [Fact]
    public async Task TryStart_remains_implemented_by_store()
    {
        // Arrange
        var store = new RecordingIdempotencyStore();
        var operation = new IdempotencyOperation(
            IdempotencyScope.From("projection:catalog-tour"),
            IdempotencyKey.From("event-42"));
        var startedAt = new DateTimeOffset(2026, 6, 21, 10, 0, 0, TimeSpan.Zero);

        // Act, Assert
        await TestAssert.Throws<NotSupportedException>(async () =>
        {
            await store.TryStart(operation, startedAt, TimeSpan.FromMinutes(5), CancellationToken.None);
        });
    }

    [Fact]
    public async Task Get_remains_implemented_by_store()
    {
        // Arrange
        var store = new RecordingIdempotencyStore();
        var operation = new IdempotencyOperation(
            IdempotencyScope.From("projection:catalog-tour"),
            IdempotencyKey.From("event-42"));

        // Act, Assert
        await TestAssert.Throws<NotSupportedException>(async () =>
        {
            await store.Get(operation, CancellationToken.None);
        });
    }
}
