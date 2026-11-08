using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Builds the exceptions a failed SQLite save produces.</summary>
public static class SqliteErrors
{
    /// <summary>Wraps a SQLite error the way EF Core does.</summary>
    /// <param name="errorCode">The primary SQLite error code.</param>
    /// <param name="extendedCode">The extended SQLite error code.</param>
    /// <param name="message">The SQLite message.</param>
    /// <returns>The update exception.</returns>
    public static DbUpdateException Wrap(int errorCode, int extendedCode, string message) =>
        new("save failed", new SqliteException(message, errorCode, extendedCode));
}
