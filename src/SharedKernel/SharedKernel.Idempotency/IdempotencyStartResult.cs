namespace SharedKernel.Idempotency;

/// <summary>
/// Describes the result of attempting to start an idempotent operation.
/// </summary>
public sealed record IdempotencyStartResult
{
    private IdempotencyStartResult(bool started, IdempotencyEntry? existingEntry)
    {
        Started = started;
        ExistingEntry = existingEntry;
    }

    /// <summary>
    /// Gets a value indicating whether the caller acquired processing ownership.
    /// </summary>
    public bool Started { get; }

    /// <summary>
    /// Gets the existing entry when processing ownership was not acquired.
    /// </summary>
    public IdempotencyEntry? ExistingEntry { get; }

    /// <summary>
    /// Creates a result for a newly started operation.
    /// </summary>
    /// <returns>The started result.</returns>
    public static IdempotencyStartResult StartedNew() => new(started: true, existingEntry: null);

    /// <summary>
    /// Creates a result for an operation that already has an entry.
    /// </summary>
    /// <param name="entry">The existing idempotency entry.</param>
    /// <returns>The already-started result.</returns>
    public static IdempotencyStartResult AlreadyStarted(IdempotencyEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new IdempotencyStartResult(started: false, existingEntry: entry);
    }
}
