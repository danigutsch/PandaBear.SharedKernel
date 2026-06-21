namespace SharedKernel.Idempotency;

/// <summary>
/// Describes the result of attempting to start an idempotent operation.
/// </summary>
/// <param name="Started">A value indicating whether the caller acquired processing ownership.</param>
/// <param name="ExistingEntry">The existing entry when processing ownership was not acquired.</param>
public sealed record IdempotencyStartResult(bool Started, IdempotencyEntry? ExistingEntry)
{
    /// <summary>
    /// Creates a result for a newly started operation.
    /// </summary>
    /// <returns>The started result.</returns>
    public static IdempotencyStartResult StartedNew() => new(true, null);

    /// <summary>
    /// Creates a result for an operation that already has an entry.
    /// </summary>
    /// <param name="entry">The existing idempotency entry.</param>
    /// <returns>The already-started result.</returns>
    public static IdempotencyStartResult AlreadyStarted(IdempotencyEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new IdempotencyStartResult(false, entry);
    }
}
