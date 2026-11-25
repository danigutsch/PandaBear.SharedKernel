namespace SharedKernel.Npgsql;

/// <summary>Identifies the PostgreSQL relations and columns used for binary artifact reconciliation.</summary>
/// <param name="MetadataSchema">The schema containing the metadata relation.</param>
/// <param name="MetadataTable">The metadata relation name.</param>
/// <param name="IdColumn">The unique, non-null UUID of each metadata row.</param>
/// <param name="EligibilityColumn">The column whose presence selects metadata rows for reconciliation.</param>
/// <param name="LengthColumn">The nullable recorded byte length.</param>
/// <param name="Sha256Column">The nullable recorded lowercase hexadecimal SHA-256 digest.</param>
/// <param name="BodySchema">The schema containing the body relation.</param>
/// <param name="BodyTable">The body relation name.</param>
/// <param name="BodyIdColumn">The unique UUID in the body relation matching the metadata identifier.</param>
/// <param name="BodyContentColumn">The <c>bytea</c> column holding the stored bytes.</param>
public sealed record PostgreSqlArtifactIntegrityMapping(
    string MetadataSchema,
    string MetadataTable,
    string IdColumn,
    string EligibilityColumn,
    string LengthColumn,
    string Sha256Column,
    string BodySchema,
    string BodyTable,
    string BodyIdColumn,
    string BodyContentColumn);
