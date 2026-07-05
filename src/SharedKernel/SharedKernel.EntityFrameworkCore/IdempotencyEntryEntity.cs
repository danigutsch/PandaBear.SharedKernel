using SharedKernel.Idempotency;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Represents a persisted idempotency entry.
/// </summary>
public sealed class IdempotencyEntryEntity
{
    /// <summary>
    /// Gets or sets the operation scope.
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operation key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operation state.
    /// </summary>
    public IdempotencyEntryState State { get; set; }

    /// <summary>
    /// Gets or sets when the operation started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the operation completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the optional result fingerprint.
    /// </summary>
    public string? ResultFingerprint { get; set; }
}
