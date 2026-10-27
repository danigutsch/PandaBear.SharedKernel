namespace PandaBear.Npgsql.Tests;

/// <summary>Queries that report details of a PostgreSQL session.</summary>
internal static class PostgreSqlSessionQueries
{
    /// <summary>Returns the current database name and user of a session opened from the data source.</summary>
    public static async Task<(string Database, string User)> GetSession(NpgsqlDataSource dataSource, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("SELECT current_database(), current_user;");
        await using var reader = await command.ExecuteReaderAsync(ct);
        var hasRow = await reader.ReadAsync(ct);
        hasRow.ShouldBeTrue("the session query returns exactly one row");
        return (reader.GetString(0), reader.GetString(1));
    }
}
