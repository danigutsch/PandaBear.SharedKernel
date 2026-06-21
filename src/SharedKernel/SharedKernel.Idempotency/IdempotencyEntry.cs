namespace SharedKernel.Idempotency;

/// <summary>
/// Represents a stored idempotency operation entry.
/// </summary>
/// <param name="Operation">The operation identity.</param>
/// <param name="State">The operation state.</param>
/// <param name="StartedAt">The time at which processing started.</param>
/// <param name="CompletedAt">The time at which processing completed, when available.</param>
/// <param name="ResultFingerprint">An optional stable fingerprint of the completed result.</param>
public sealed record IdempotencyEntry(
    IdempotencyOperation Operation,
    IdempotencyEntryState State,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? ResultFingerprint);
