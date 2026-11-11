using System.Globalization;
using Microsoft.Data.Sqlite;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Creates and finds a <c>tours</c> table through short-lived pooled connections, as application code does.</summary>
internal static class SqliteTables
{
    /// <summary>Creates the <c>tours</c> table.</summary>
    /// <param name="connectionString">The database.</param>
    internal static void CreateTours(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE tours (id INTEGER PRIMARY KEY)";
        command.ExecuteNonQuery();
    }

    /// <summary>Gets whether the <c>tours</c> table exists.</summary>
    /// <param name="connectionString">The database.</param>
    /// <returns>Whether it exists.</returns>
    internal static bool ToursExist(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'tours'";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }
}
