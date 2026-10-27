namespace PandaBear.Idempotency.Tests;

/// <summary>
/// An IIdempotencyStore test double that records the arguments of Complete and throws NotSupportedException for the other operations.
/// </summary>
internal sealed class RecordingIdempotencyStore : IIdempotencyStore
{
    /// <summary>
    /// Gets the operation passed to the last Complete call, or null before any call.
    /// </summary>
    public IdempotencyOperation? CompletedOperation
    {
        get; private set;
    }

    /// <summary>
    /// Gets the completion time passed to the last Complete call, or null before any call.
    /// </summary>
    public DateTimeOffset? CompletedAt
    {
        get; private set;
    }

    /// <summary>
    /// Gets the result fingerprint passed to the last Complete call, or null.
    /// </summary>
    public string? ResultFingerprint
    {
        get; private set;
    }

    /// <summary>
    /// Gets the cancellation token passed to the last Complete call.
    /// </summary>
    public CancellationToken CancellationToken
    {
        get; private set;
    }

    /// <summary>
    /// Gets how many times Complete was called.
    /// </summary>
    public int CompleteCallCount
    {
        get; private set;
    }

    /// <summary>
    /// Always throws NotSupportedException because the tests never start operations through this store.
    /// </summary>
    public ValueTask<IdempotencyStartResult> TryStart(
        IdempotencyOperation operation,
        DateTimeOffset startedAt,
        TimeSpan? lockDuration,
        CancellationToken ct) => throw new NotSupportedException();

    /// <summary>
    /// Records the operation, completion time, fingerprint and cancellation token and increments the call count.
    /// </summary>
    public ValueTask Complete(
        IdempotencyOperation operation,
        DateTimeOffset completedAt,
        string? resultFingerprint,
        CancellationToken ct)
    {
        CompleteCallCount++;
        CompletedOperation = operation;
        CompletedAt = completedAt;
        ResultFingerprint = resultFingerprint;
        CancellationToken = ct;

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Forwards to Complete so staged completions are recorded the same way.
    /// </summary>
    public ValueTask StageCompletion(
        IdempotencyOperation operation,
        DateTimeOffset completedAt,
        string? resultFingerprint,
        CancellationToken ct) => Complete(operation, completedAt, resultFingerprint, ct);

    /// <summary>
    /// Always throws NotSupportedException because the tests never read entries through this store.
    /// </summary>
    public ValueTask<IdempotencyEntry?> Get(IdempotencyOperation operation, CancellationToken ct) =>
        throw new NotSupportedException();
}
