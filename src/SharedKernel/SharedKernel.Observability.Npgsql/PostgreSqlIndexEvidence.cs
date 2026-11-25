namespace SharedKernel.Observability.Npgsql;

/// <summary>Contains read-only PostgreSQL catalog evidence for one index-health assessment.</summary>
/// <remarks>Object names are advisory evidence. Callers must not add them to telemetry or logs.</remarks>
public sealed record PostgreSqlIndexEvidence
{
    /// <summary>Gets the kind of catalog object assessed.</summary>
    public required PostgreSqlIndexEvidenceKind Kind { get; init; }

    /// <summary>Gets the schema that owns the assessed object.</summary>
    public required string SchemaName { get; init; }

    /// <summary>Gets the table associated with the assessed object.</summary>
    public required string TableName { get; init; }

    /// <summary>Gets the index name when the assessed object is an index.</summary>
    public string? IndexName { get; init; }

    /// <summary>Gets the elapsed period represented by PostgreSQL cumulative statistics.</summary>
    public required TimeSpan? StatisticsWindow { get; init; }

    /// <summary>Gets the index or sequential scan count observed by PostgreSQL.</summary>
    public required long ScanCount { get; init; }

    /// <summary>Gets the tuple-read count observed by PostgreSQL.</summary>
    public required long TuplesRead { get; init; }

    /// <summary>Gets the tuple-fetch count observed by PostgreSQL for an index.</summary>
    public required long TuplesFetched { get; init; }

    /// <summary>Gets PostgreSQL's current row-count estimate for the table.</summary>
    public required long EstimatedRows { get; init; }

    /// <summary>Gets a value indicating whether a constraint or uniqueness rule protects the index.</summary>
    public required bool IsProtected { get; init; }

    /// <summary>Gets a value indicating whether PostgreSQL reports the index as valid, ready, and live.</summary>
    public required bool IsUsable { get; init; }

    /// <summary>Gets a value indicating whether the index is neither partial nor expression-based.</summary>
    public required bool IsSimple { get; init; }

    /// <summary>Gets a value indicating whether PostgreSQL has analyzed the table since statistics reset.</summary>
    public required bool StatisticsAreReliable { get; init; }
}
