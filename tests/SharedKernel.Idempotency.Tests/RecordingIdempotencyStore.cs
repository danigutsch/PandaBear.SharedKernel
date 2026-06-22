namespace SharedKernel.Idempotency.Tests;

internal sealed class RecordingIdempotencyStore : IIdempotencyStore
{
    public IdempotencyOperation? CompletedOperation { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? ResultFingerprint { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public int CompleteCallCount { get; private set; }

    public ValueTask<IdempotencyStartResult> TryStart(
        IdempotencyOperation operation,
        DateTimeOffset startedAt,
        TimeSpan? lockDuration,
        CancellationToken ct) => throw new NotSupportedException();

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

    public ValueTask<IdempotencyEntry?> Get(IdempotencyOperation operation, CancellationToken ct) =>
        throw new NotSupportedException();
}
