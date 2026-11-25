namespace SharedKernel.Npgsql.Tests;

/// <summary>Creates scheduler relations and obtains timestamps from the synthetic database.</summary>
internal static class PostgreSqlJobSchedulerTestData
{
    /// <summary>The isolated mixed-case schema used by scheduler tests.</summary>
    public const string Schema = "Job Scheduling";

    /// <summary>Creates an application-owned schema and initializes it twice.</summary>
    public static async Task<PostgreSqlJobScheduler> CreateScheduler(NpgsqlDataSource dataSource, CancellationToken ct)
    {
        await using var schema = dataSource.CreateCommand("""CREATE SCHEMA "Job Scheduling" """);
        await schema.ExecuteNonQueryAsync(ct);
        var scheduler = new PostgreSqlJobScheduler(dataSource, Schema);
        await scheduler.InitializeSchema(ct);
        await scheduler.InitializeSchema(ct);
        return scheduler;
    }

    /// <summary>Returns the current UTC timestamp from PostgreSQL.</summary>
    public static async Task<DateTimeOffset> DatabaseTime(NpgsqlDataSource dataSource, CancellationToken ct)
    {
        await using var query = dataSource.CreateCommand("SELECT pg_catalog.clock_timestamp()");
        return new DateTimeOffset((DateTime)(await query.ExecuteScalarAsync(ct))!);
    }
}
