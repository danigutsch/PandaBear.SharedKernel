using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Sqlite;

/// <summary>
/// Recognizes SQLite unique and primary-key violations. SQLite reports the violated columns as <c>Table.Column</c> (or an
/// index name for expression indexes) in the error text, never a constraint name, so those are what it reports.
/// </summary>
public sealed class SqliteUniqueViolationParser : IUniqueViolationParser
{
    private const int ConstraintError = 19;
    private const int UniqueExtendedError = 2067;
    private const int PrimaryKeyExtendedError = 1555;
    private const string Marker = "UNIQUE constraint failed: ";
    private const string IndexPrefix = "index '";

    /// <inheritdoc />
    public bool TryParse(DbUpdateException exception, out UniqueViolation violation)
    {
        ArgumentNullException.ThrowIfNull(exception);

        violation = new UniqueViolation(null, null, []);
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SqliteException { SqliteErrorCode: ConstraintError, SqliteExtendedErrorCode: UniqueExtendedError or PrimaryKeyExtendedError } sqlite
                && TryRead(sqlite.Message, out violation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryRead(string message, out UniqueViolation violation)
    {
        violation = new UniqueViolation(null, null, []);
        var start = message.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        var detail = message[(start + Marker.Length)..];
        if (detail.StartsWith(IndexPrefix, StringComparison.Ordinal))
        {
            var name = detail[IndexPrefix.Length..];
            var end = name.IndexOf('\'', StringComparison.Ordinal);
            if (end <= 0)
            {
                return false;
            }

            violation = new UniqueViolation(name[..end], null, []);
            return true;
        }

        var quote = detail.IndexOf('\'', StringComparison.Ordinal);
        var parts = (quote >= 0 ? detail[..quote] : detail).Split(", ", StringSplitOptions.RemoveEmptyEntries);
        string? table = null;
        var columns = new List<string>();
        foreach (var part in parts)
        {
            var dot = part.IndexOf('.', StringComparison.Ordinal);
            if (dot <= 0 || dot == part.Length - 1)
            {
                return false;
            }

            table ??= part[..dot];
            columns.Add(part[(dot + 1)..]);
        }

        if (table is null)
        {
            return false;
        }

        violation = new UniqueViolation(null, table, columns);
        return true;
    }
}
