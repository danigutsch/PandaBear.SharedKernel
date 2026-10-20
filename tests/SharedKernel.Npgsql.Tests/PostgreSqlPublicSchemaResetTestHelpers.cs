using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

internal static class PostgreSqlPublicSchemaResetTestHelpers
{
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
