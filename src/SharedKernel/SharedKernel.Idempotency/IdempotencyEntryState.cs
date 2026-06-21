namespace SharedKernel.Idempotency;

/// <summary>
/// Describes the current state of an idempotent operation.
/// </summary>
public enum IdempotencyEntryState
{
    /// <summary>
    /// The operation has started but has not completed.
    /// </summary>
    Started = 0,

    /// <summary>
    /// The operation completed successfully and can be treated as replayed on duplicate delivery.
    /// </summary>
    Completed = 1
}
