using System.Data.Common;

namespace SharedKernel.IntegrationTesting;

/// <summary>
/// Resets PostgreSQL public-schema tables to a known baseline for integration tests.
/// </summary>
public static class PostgreSqlPublicSchemaReset
{
    private const string ResetPublicTablesSql = """
                                                 CREATE OR REPLACE FUNCTION pg_temp."ResetPublicSchema"(excluded_table_names text[])
                                                 RETURNS void
                                                 LANGUAGE plpgsql
                                                 AS $$
                                                 DECLARE
                                                     tables_to_truncate text;
                                                 BEGIN
                                                     SELECT string_agg(format('%I.%I', schemaname, tablename), ', ')
                                                     INTO tables_to_truncate
                                                     FROM pg_catalog.pg_tables
                                                     WHERE schemaname = 'public'
                                                       AND tablename NOT LIKE '__EFMigrationsHistory%'
                                                       AND NOT (tablename = ANY(excluded_table_names));

                                                     IF tables_to_truncate IS NOT NULL THEN
                                                         EXECUTE 'TRUNCATE TABLE ' || tables_to_truncate || ' RESTART IDENTITY RESTRICT';
                                                     END IF;
                                                 END;
                                                 $$;

                                                 SELECT pg_temp."ResetPublicSchema"(@excluded_table_names);
                                                 """;

    /// <summary>
    /// Truncates all public-schema tables except EF migrations history tables.
    /// </summary>
    /// <param name="connection">The PostgreSQL database connection.</param>
    /// <param name="ct">A cancellation token.</param>
    public static async Task Reset(DbConnection connection, CancellationToken ct)
    {
        await Reset(connection, [], ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Truncates public-schema tables except EF migrations history and caller-selected immutable tables.
    /// </summary>
    /// <param name="connection">The PostgreSQL database connection.</param>
    /// <param name="excludedTableNames">Public-schema table names to preserve.</param>
    /// <param name="ct">A cancellation token.</param>
    public static async Task Reset(
        DbConnection connection,
        IReadOnlyCollection<string> excludedTableNames,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(excludedTableNames);

        if (excludedTableNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Excluded table names cannot be empty.", nameof(excludedTableNames));
        }

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
        }

        using var command = connection.CreateCommand();
        command.CommandText = ResetPublicTablesSql;
        var excludedTableNamesParameter = command.CreateParameter();
        excludedTableNamesParameter.ParameterName = "excluded_table_names";
        excludedTableNamesParameter.Value = excludedTableNames.ToArray();
        _ = command.Parameters.Add(excludedTableNamesParameter);

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
