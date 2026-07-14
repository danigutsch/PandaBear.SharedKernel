using System.Data.Common;

namespace SharedKernel.IntegrationTesting;

/// <summary>
/// Resets PostgreSQL public-schema tables to a known baseline for integration tests.
/// </summary>
public static class PostgreSqlPublicSchemaReset
{
    private const string ResetPublicTablesSql = """
                                                DO $$
                                                DECLARE
                                                    tables_to_truncate text;
                                                BEGIN
                                                    SELECT string_agg(format('%I.%I', schemaname, tablename), ', ')
                                                    INTO tables_to_truncate
                                                    FROM pg_catalog.pg_tables
                                                    WHERE schemaname = 'public'
                                                      AND tablename NOT LIKE '__EFMigrationsHistory%';

                                                    IF tables_to_truncate IS NOT NULL THEN
                                                        EXECUTE 'TRUNCATE TABLE ' || tables_to_truncate || ' RESTART IDENTITY CASCADE';
                                                    END IF;
                                                END $$;
                                                """;

    /// <summary>
    /// Truncates all public-schema tables except EF migrations history tables.
    /// </summary>
    /// <param name="connection">The PostgreSQL database connection.</param>
    /// <param name="ct">A cancellation token.</param>
    public static async Task Reset(DbConnection connection, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
        }

        using var command = connection.CreateCommand();
        command.CommandText = ResetPublicTablesSql;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
