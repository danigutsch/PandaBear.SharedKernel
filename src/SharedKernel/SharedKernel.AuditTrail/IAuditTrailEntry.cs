namespace SharedKernel.AuditTrail;

/// <summary>Represents a metadata-only entry that belongs in an application-owned audit trail.</summary>
public interface IAuditTrailEntry
{
    /// <summary>Gets when the auditable operation occurred in UTC.</summary>
    DateTime OccurredAtUtc { get; }
}
