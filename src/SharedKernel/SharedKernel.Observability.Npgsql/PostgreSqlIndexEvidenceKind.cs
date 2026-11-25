namespace SharedKernel.Observability.Npgsql;

/// <summary>Identifies the catalog object represented by index-health evidence.</summary>
public enum PostgreSqlIndexEvidenceKind
{
    /// <summary>Evidence about an existing index.</summary>
    Index,

    /// <summary>Evidence about a table whose sequential scans merit human index review.</summary>
    Table,
}
