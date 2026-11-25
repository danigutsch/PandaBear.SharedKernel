namespace SharedKernel.AuditTrail;

/// <summary>Appends audit entries at an application-owned persistence boundary.</summary>
/// <typeparam name="TAuditTrailEntry">The metadata-only audit entry type.</typeparam>
public interface IAuditTrailSink<in TAuditTrailEntry>
    where TAuditTrailEntry : IAuditTrailEntry
{
    /// <summary>Appends an audit entry to the current atomic persistence operation.</summary>
    /// <param name="entry">The metadata-only audit entry to append.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A task that completes when the entry has joined the current operation.</returns>
    ValueTask Append(TAuditTrailEntry entry, CancellationToken ct);
}
