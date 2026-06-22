namespace SharedKernel.Idempotency.Tests;

public sealed class IIdempotencyStoreTests
{
    [Fact]
    public async Task Complete_Without_Result_Fingerprint_Forwards_Null_Fingerprint()
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
        Assert.Equal(operation, store.CompletedOperation);
        Assert.Equal(completedAt, store.CompletedAt);
        Assert.Null(store.ResultFingerprint);
        Assert.Equal(cts.Token, store.CancellationToken);
        Assert.Equal(1, store.CompleteCallCount);
    }

    [Fact]
    public async Task Complete_With_Result_Fingerprint_Forwards_Fingerprint()
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
        Assert.Equal(operation, store.CompletedOperation);
        Assert.Equal(completedAt, store.CompletedAt);
        Assert.Equal("sha256:booking-response", store.ResultFingerprint);
        Assert.Equal(cts.Token, store.CancellationToken);
        Assert.Equal(1, store.CompleteCallCount);
    }

    [Fact]
    public async Task Complete_With_Null_Result_Fingerprint_Preserves_Null_Fingerprint()
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
        Assert.Equal(operation, store.CompletedOperation);
        Assert.Equal(completedAt, store.CompletedAt);
        Assert.Null(store.ResultFingerprint);
        Assert.Equal(CancellationToken.None, store.CancellationToken);
        Assert.Equal(1, store.CompleteCallCount);
    }

    [Fact]
    public async Task TryStart_Remains_Implemented_By_Store()
    {
        // Arrange
        var store = new RecordingIdempotencyStore();
        var operation = new IdempotencyOperation(
            IdempotencyScope.From("projection:catalog-tour"),
            IdempotencyKey.From("event-42"));
        var startedAt = new DateTimeOffset(2026, 6, 21, 10, 0, 0, TimeSpan.Zero);

        // Act, Assert
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await store.TryStart(operation, startedAt, TimeSpan.FromMinutes(5), CancellationToken.None);
        });
    }

    [Fact]
    public async Task Get_Remains_Implemented_By_Store()
    {
        // Arrange
        var store = new RecordingIdempotencyStore();
        var operation = new IdempotencyOperation(
            IdempotencyScope.From("projection:catalog-tour"),
            IdempotencyKey.From("event-42"));

        // Act, Assert
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await store.Get(operation, CancellationToken.None);
        });
    }
}
