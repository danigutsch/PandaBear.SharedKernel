using System.Diagnostics.CodeAnalysis;

namespace PandaBear.Npgsql.Tests;

/// <summary>A throwaway login role with a generated password, dropped when disposed.</summary>
/// <remarks>
/// A role belongs to the server rather than to one database, so a lease never drops it. Dispose it after the leases
/// whose databases it connected to and before the database that holds the administrative data source.
/// </remarks>
internal sealed class TemporaryPostgreSqlRole : IAsyncDisposable
{
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(30);

    private readonly NpgsqlDataSource _administrativeDataSource;

    private TemporaryPostgreSqlRole(NpgsqlDataSource administrativeDataSource, string name, string password)
    {
        _administrativeDataSource = administrativeDataSource;
        Name = name;
        Password = password;
    }

    /// <summary>Gets the generated role name.</summary>
    public string Name
    {
        get;
    }

    /// <summary>Gets the generated password of the role.</summary>
    public string Password
    {
        get;
    }

    /// <summary>Creates a login role with a generated name and password through the administrative data source.</summary>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The role name and password use a fixed ASCII prefix and Guid.NewGuid().ToString(\"N\").")]
    public static async Task<TemporaryPostgreSqlRole> Create(NpgsqlDataSource administrativeDataSource, CancellationToken ct)
    {
        var name = $"lease_role_{Guid.NewGuid():N}";
        var password = Guid.NewGuid().ToString("N");
        await using var command = administrativeDataSource.CreateCommand($"CREATE ROLE \"{name}\" LOGIN PASSWORD '{password}';");
        _ = await command.ExecuteNonQueryAsync(ct);
        return new TemporaryPostgreSqlRole(administrativeDataSource, name, password);
    }

    /// <summary>Drops the role, if it still exists, through the administrative data source with a 30 second timeout.</summary>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The role name uses a fixed ASCII prefix and Guid.NewGuid().ToString(\"N\").")]
    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(CleanupTimeout);
        await using var command = _administrativeDataSource.CreateCommand($"DROP ROLE IF EXISTS \"{Name}\";");
        _ = await command.ExecuteNonQueryAsync(timeout.Token);
    }
}
