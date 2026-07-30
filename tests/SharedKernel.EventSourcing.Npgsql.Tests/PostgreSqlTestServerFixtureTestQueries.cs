namespace SharedKernel.EventSourcing.Npgsql.Tests;

internal static class PostgreSqlTestServerFixtureTestQueries
{
    public static async Task<string> GetServerIdentity(NpgsqlDataSource dataSource, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT pg_postmaster_start_time()::text || ':' || inet_server_port()::text;");
        return (string)(await command.ExecuteScalarAsync(ct)).ShouldNotBeNull();
    }

    public static async Task CreateOwnedTable(NpgsqlDataSource dataSource, string value, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(
            "CREATE TABLE public.lease_owned_table (value text NOT NULL); "
            + "INSERT INTO public.lease_owned_table (value) VALUES (@value);");
        command.Parameters.AddWithValue("value", value);
        _ = await command.ExecuteNonQueryAsync(ct);
    }

    public static async Task<string> ReadOwnedValue(NpgsqlDataSource dataSource, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("SELECT value FROM public.lease_owned_table;");
        return (string)(await command.ExecuteScalarAsync(ct)).ShouldNotBeNull();
    }
}
