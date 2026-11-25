namespace SharedKernel.Npgsql;

/// <summary>Identifies why a scheduler execution was created.</summary>
public enum PostgreSqlJobRunKind
{
    /// <summary>The configured schedule became due.</summary>
    Scheduled = 0,

    /// <summary>An explicit request triggered the job.</summary>
    Manual = 1,

    /// <summary>An explicit request retried a failed run.</summary>
    Retry = 2,
}
