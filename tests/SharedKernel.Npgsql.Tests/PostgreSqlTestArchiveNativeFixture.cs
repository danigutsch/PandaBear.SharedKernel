using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SharedKernel.Npgsql.Tests;

/// <summary>Supplies pinned native clients and SQL controls for the live archive rehearsal.</summary>
internal sealed class PostgreSqlTestArchiveNativeFixture
{
    private const string ClientsVariable = "PANDABEAR_TEST_POSTGRESQL_CLIENTS";

    private readonly string _clientDirectory;

    private PostgreSqlTestArchiveNativeFixture(string clientDirectory)
    {
        _clientDirectory = clientDirectory;
    }

    /// <summary>Reads the caller-provisioned directory of compatible PostgreSQL native clients.</summary>
    public static PostgreSqlTestArchiveNativeFixture FromEnvironment()
    {
        var directory = Environment.GetEnvironmentVariable(ClientsVariable);
        if (directory is null || !Path.IsPathFullyQualified(directory))
        {
            throw new InvalidOperationException($"Set {ClientsVariable} to the pinned native client directory.");
        }

        return new PostgreSqlTestArchiveNativeFixture(directory);
    }

    /// <summary>Supplies the client command and connection environment for a fixture database.</summary>
    public ProcessStartInfo Start(string tool, PostgreSqlTestDatabaseLease database)
    {
        var executable = OperatingSystem.IsWindows() ? tool + ".exe" : tool;
        var command = new ProcessStartInfo(Path.Combine(_clientDirectory, executable));
        var connection = new NpgsqlConnectionStringBuilder(database.ConnectionString);
        command.Environment["PGHOST"] = connection.Host;
        command.Environment["PGPORT"] = connection.Port.ToString(CultureInfo.InvariantCulture);
        command.Environment["PGUSER"] = connection.Username;
        command.Environment["PGPASSWORD"] = connection.Password;
        return command;
    }

    /// <summary>Creates an ordinary table that restores before the owner-dependent fixture table.</summary>
    public static async Task CreateMarker(NpgsqlDataSource database, CancellationToken ct)
    {
        await using var command = database.CreateCommand(
            "CREATE TABLE public.archive_recovery_marker (value integer NOT NULL); "
            + "INSERT INTO public.archive_recovery_marker (value) VALUES (1);");
        _ = await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Assigns the fixture table to the generated role so that restoring requires that role.</summary>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The generated fixture role is quoted as a PostgreSQL identifier.")]
    public static async Task AssignOwner(NpgsqlDataSource database, TemporaryPostgreSqlRole owner, CancellationToken ct)
    {
        var identifier = OwnedIdentifier(owner);
        await using var command = database.CreateCommand($"ALTER TABLE public.lease_owned_table OWNER TO {identifier};");
        _ = await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Re-creates the same generated owner after its expected absence interrupted restore.</summary>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The generated fixture role is quoted as a PostgreSQL identifier.")]
    public static async Task RecreateOwner(NpgsqlDataSource database, TemporaryPostgreSqlRole owner, CancellationToken ct)
    {
        var identifier = OwnedIdentifier(owner);
        await using var command = database.CreateCommand($"CREATE ROLE {identifier} NOLOGIN;");
        _ = await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Checks whether a failed restore rolled back both fixture tables.</summary>
    public static async Task<bool> TablesAreAbsent(NpgsqlDataSource database, CancellationToken ct)
    {
        await using var command = database.CreateCommand(
            "SELECT to_regclass('public.archive_recovery_marker') IS NULL "
            + "AND to_regclass('public.lease_owned_table') IS NULL;");
        return await command.ExecuteScalarAsync(ct) is true;
    }

    /// <summary>Reads the earlier marker from the successful restore.</summary>
    public static async Task<int> ReadMarker(NpgsqlDataSource database, CancellationToken ct)
    {
        await using var command = database.CreateCommand("SELECT value FROM public.archive_recovery_marker;");
        return (int)(await command.ExecuteScalarAsync(ct)).ShouldNotBeNull();
    }

    /// <summary>Gets the restored table's actual PostgreSQL owner.</summary>
    public static async Task<string> ReadOwner(NpgsqlDataSource database, CancellationToken ct)
    {
        await using var command = database.CreateCommand(
            "SELECT pg_get_userbyid(relowner) FROM pg_class WHERE oid = to_regclass('public.lease_owned_table');");
        return (string)(await command.ExecuteScalarAsync(ct)).ShouldNotBeNull();
    }

    private static string OwnedIdentifier(TemporaryPostgreSqlRole owner)
    {
        using var builder = new NpgsqlCommandBuilder();
        return builder.QuoteIdentifier(owner.Name);
    }
}
