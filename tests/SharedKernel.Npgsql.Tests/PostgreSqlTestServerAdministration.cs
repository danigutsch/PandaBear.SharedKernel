namespace PandaBear.Npgsql.Tests;

/// <summary>Administrative operations on the PostgreSQL test server that tests use to provoke specific cleanup behavior.</summary>
internal static class PostgreSqlTestServerAdministration
{
    /// <summary>
    /// Marks a fixture-issued database as a template, or clears the mark. PostgreSQL refuses to drop a template database,
    /// which gives a test a database whose cleanup deterministically fails and succeeds again once the mark is cleared.
    /// </summary>
    public static async Task SetTemplateFlag(string serverConnectionString, string databaseName, bool isTemplate, CancellationToken ct)
    {
        const string prefix = "test_";
        if (!databaseName.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(databaseName[prefix.Length..], "N", out _))
        {
            throw new ArgumentException("The database was not issued by the fixture.", nameof(databaseName));
        }

        var administrativeConnectionString = new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = "postgres" }.ConnectionString;
        await using var dataSource = NpgsqlDataSource.Create(administrativeConnectionString);
        await using var command = dataSource.CreateCommand($"ALTER DATABASE \"{databaseName}\" WITH IS_TEMPLATE {(isTemplate ? "true" : "false")};");
        _ = await command.ExecuteNonQueryAsync(ct);
    }
}
