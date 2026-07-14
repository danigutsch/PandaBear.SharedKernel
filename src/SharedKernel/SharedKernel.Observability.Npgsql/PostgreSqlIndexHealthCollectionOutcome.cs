namespace SharedKernel.Observability.Npgsql;

/// <summary>Describes the bounded outcome of a PostgreSQL index-health collection attempt.</summary>
public enum PostgreSqlIndexHealthCollectionOutcome
{
    /// <summary>Read-only catalog evidence was collected.</summary>
    Collected,

    /// <summary>The monitoring role could not read the required PostgreSQL statistics.</summary>
    PermissionDenied,

    /// <summary>The connected PostgreSQL server does not support the required capability.</summary>
    Unsupported,

    /// <summary>The database was unavailable or did not complete collection in time.</summary>
    Unavailable,
}
