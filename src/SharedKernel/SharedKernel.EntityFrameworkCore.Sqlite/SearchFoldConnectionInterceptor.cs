using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PandaBear.EntityFrameworkCore.Sqlite;

/// <summary>Registers <see cref="SearchFold.FunctionName"/> on every SQLite connection a context opens.</summary>
internal sealed class SearchFoldConnectionInterceptor : DbConnectionInterceptor
{
    /// <summary>Gets the shared instance; it keeps no state.</summary>
    internal static SearchFoldConnectionInterceptor Instance { get; } = new();

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => Register(connection);

    /// <inheritdoc />
    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Register(connection);
        return Task.CompletedTask;
    }

    private static void Register(DbConnection connection)
    {
        if (connection is SqliteConnection sqlite)
        {
            SearchFold.Register(sqlite);
        }
    }
}
