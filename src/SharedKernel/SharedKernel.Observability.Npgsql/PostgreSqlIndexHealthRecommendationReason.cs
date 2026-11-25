namespace SharedKernel.Observability.Npgsql;

/// <summary>Explains the bounded reason for an index-health assessment.</summary>
public enum PostgreSqlIndexHealthRecommendationReason
{
    /// <summary>PostgreSQL had no usable statistics reset timestamp.</summary>
    StatisticsUnavailable,

    /// <summary>The statistics period is too short for an advisory recommendation.</summary>
    StatisticsWindowTooShort,

    /// <summary>The table estimate is too small for this conservative policy.</summary>
    TableTooSmall,

    /// <summary>A constraint or uniqueness rule protects the index.</summary>
    ProtectedIndex,

    /// <summary>The index is invalid, not ready, dead, partial, or expression-based.</summary>
    UnsupportedIndexShape,

    /// <summary>Per-object counter resets prevent establishing a safe index observation window.</summary>
    PerObjectStatisticsWindowUnavailable,

    /// <summary>Index tuple reads are high relative to the estimated table size.</summary>
    HighIndexReadVolume,

    /// <summary>Sequential scan tuple reads are high relative to the estimated table size.</summary>
    HighSequentialScanVolume,

    /// <summary>Observed workload does not meet the policy's review threshold.</summary>
    InsufficientActivity,
}
