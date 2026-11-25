namespace SharedKernel.Npgsql.Tests;

/// <summary>Provides scheduling history schema and deterministic identifiers for PostgreSQL tests.</summary>
internal static class PostgreSqlScheduleRunStoreTestData
{
    /// <summary>The fixture schema name, including a space to exercise identifier quoting.</summary>
    public const string Schema = "Scheduling ops";

    /// <summary>The fixture table name.</summary>
    public const string Table = "Run history";

    /// <summary>Gets a fixture UUID that sorts in the same order as its numeric suffix.</summary>
    public static Guid TestId(int suffix) => Guid.Parse(FormattableString.Invariant($"00000000-0000-0000-0000-{suffix:D12}"));

    /// <summary>Creates an isolated provider table with additional columns outside the operator projection.</summary>
    public static async Task CreateTable(NpgsqlDataSource dataSource, CancellationToken ct)
    {
        await using var setup = dataSource.CreateCommand(
            """
            CREATE SCHEMA "Scheduling ops";
            CREATE TABLE "Scheduling ops"."Run history" (
                job_name text COLLATE "C" NOT NULL,
                run_id uuid PRIMARY KEY,
                started_at timestamptz NOT NULL,
                scheduled_for timestamptz,
                observed_until timestamptz NOT NULL,
                finished_at timestamptz,
                outcome integer,
                retry_at timestamptz,
                private_arguments text NOT NULL DEFAULT 'private-arguments',
                raw_error text NOT NULL DEFAULT 'raw-error',
                credentials text NOT NULL DEFAULT 'credentials',
                CHECK (observed_until >= started_at),
                CHECK (
                    (finished_at IS NULL AND outcome IS NULL AND retry_at IS NULL)
                    OR (finished_at >= started_at AND finished_at IS NOT NULL AND outcome IS NOT NULL
                        AND outcome BETWEEN 2 AND 6 AND ((outcome = 5) = (retry_at IS NOT NULL)))
                )
            );
            CREATE INDEX run_history_recent ON "Scheduling ops"."Run history"
                (job_name, started_at DESC, run_id DESC);
            """);
        _ = await setup.ExecuteNonQueryAsync(ct);
    }
}
