using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

/// <summary>Helpers for the public schema reset tests.</summary>
internal static class PostgreSqlPublicSchemaResetTestHelpers
{
    /// <summary>Resets the public schema while excluding the table excluded_child, returning the PostgreSQL error the reset raises or null if it succeeds.</summary>
    public static async Task<PostgresException?> CaptureExcludedChildResetFailure(
        NpgsqlConnection connection,
        CancellationToken ct)
    {
        try
        {
            await PostgreSqlPublicSchemaReset.Reset(connection, ["excluded_child"], ct);
            return null;
        }
        catch (PostgresException exception)
        {
            return exception;
        }
    }
}
