namespace PandaBear.Npgsql.Tests;

internal static class PostgreSqlSessionQueries
{
    public static async Task<(string Database, string User)> GetSession(NpgsqlDataSource dataSource, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("SELECT current_database(), current_user;");
        await using var reader = await command.ExecuteReaderAsync(ct);
        var hasRow = await reader.ReadAsync(ct);
        hasRow.ShouldBeTrue("the session query returns exactly one row");
        return (reader.GetString(0), reader.GetString(1));
    }
}
