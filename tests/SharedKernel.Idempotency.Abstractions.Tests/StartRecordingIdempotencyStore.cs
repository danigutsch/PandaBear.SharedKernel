namespace PandaBear.Idempotency.Tests;

/// <summary>
/// An IIdempotencyStore that predates retention owners: it implements only the four-argument TryStart and records its arguments.
/// </summary>
internal sealed class StartRecordingIdempotencyStore : IIdempotencyStore
{
    /// <summary>
    /// Gets how many times the four-argument TryStart was called.
    /// </summary>
    public int StartCallCount
    {
        get; private set;
    }

    /// <summary>
    /// Gets the operation passed to the last TryStart call, or null before any call.
    /// </summary>
    public IdempotencyOperation? StartedOperation
    {
        get; private set;
    }

    /// <summary>
    /// Records the call and reports a new start.
    /// </summary>
    public ValueTask<IdempotencyStartResult> TryStart(
        IdempotencyOperation operation,
        DateTimeOffset startedAt,
        TimeSpan? lockDuration,
        CancellationToken ct)
    {
        StartCallCount++;
        StartedOperation = operation;
        return ValueTask.FromResult(IdempotencyStartResult.StartedNew());
    }

    /// <summary>
    /// Always throws NotSupportedException because the tests never complete operations through this store.
    /// </summary>
    public ValueTask Complete(
        IdempotencyOperation operation,
        DateTimeOffset completedAt,
        string? resultFingerprint,
        CancellationToken ct) => throw new NotSupportedException();

    /// <summary>
    /// Always throws NotSupportedException because the tests never stage completions through this store.
    /// </summary>
    public ValueTask StageCompletion(
        IdempotencyOperation operation,
        DateTimeOffset completedAt,
        string? resultFingerprint,
        CancellationToken ct) => throw new NotSupportedException();

    /// <summary>
    /// Always throws NotSupportedException because the tests never read entries through this store.
    /// </summary>
    public ValueTask<IdempotencyEntry?> Get(IdempotencyOperation operation, CancellationToken ct) =>
        throw new NotSupportedException();
}
