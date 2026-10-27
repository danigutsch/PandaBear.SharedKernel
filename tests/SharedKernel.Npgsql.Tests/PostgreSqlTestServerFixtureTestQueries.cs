namespace PandaBear.Npgsql.Tests;

/// <summary>SQL helpers used by the server fixture tests to identify the server and exercise a per-lease table.</summary>
internal static class PostgreSqlTestServerFixtureTestQueries
{
    /// <summary>Returns the server's postmaster start time and port as text, which identifies the server instance behind a data source.</summary>
    public static async Task<string> GetServerIdentity(NpgsqlDataSource dataSource, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT pg_postmaster_start_time()::text || ':' || inet_server_port()::text;");
        return (string)(await command.ExecuteScalarAsync(ct)).ShouldNotBeNull();
    }

    /// <summary>Creates public.lease_owned_table in the data source's database and inserts the given value.</summary>
    public static async Task CreateOwnedTable(NpgsqlDataSource dataSource, string value, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(
            "CREATE TABLE public.lease_owned_table (value text NOT NULL); "
            + "INSERT INTO public.lease_owned_table (value) VALUES (@value);");
        command.Parameters.AddWithValue("value", value);
        _ = await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Reads the value stored in public.lease_owned_table of the data source's database.</summary>
    public static async Task<string> ReadOwnedValue(NpgsqlDataSource dataSource, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("SELECT value FROM public.lease_owned_table;");
        return (string)(await command.ExecuteScalarAsync(ct)).ShouldNotBeNull();
    }
}
