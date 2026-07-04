namespace SharedKernel.BuildingBlocks;

/// <summary>
/// Provides helpers for compensating a side effect when the following operation fails.
/// </summary>
public static class Compensation
{
    /// <summary>
    /// Runs an operation and invokes a compensation action if the operation fails.
    /// </summary>
    /// <param name="operation">The operation that must complete after an earlier side effect.</param>
    /// <param name="compensate">The action that compensates the earlier side effect when the operation fails.</param>
    /// <param name="ct">A token that can cancel the operation.</param>
    /// <returns>A task that completes when the operation succeeds or compensation has run after failure.</returns>
    public static async ValueTask CompleteOrCompensate(
        Func<CancellationToken, ValueTask> operation,
        Func<CancellationToken, ValueTask> compensate,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(compensate);

        try
        {
            await operation(ct).ConfigureAwait(false);
        }
        catch
        {
            await compensate(ct).ConfigureAwait(false);
            throw;
        }
    }
}
