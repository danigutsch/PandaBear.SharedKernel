using Npgsql;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>Builds the provider exceptions the unique-violation parser reads, without a database.</summary>
internal static class UniqueViolationTestExceptions
{
    /// <summary>Creates a PostgreSQL error as the server would report it.</summary>
    /// <param name="sqlState">The SQLSTATE.</param>
    /// <param name="constraintName">The violated constraint, or <see langword="null"/>.</param>
    /// <param name="tableName">The table.</param>
    /// <returns>The exception.</returns>
    internal static PostgresException Postgres(string sqlState, string? constraintName, string tableName) =>
        new(
            messageText: "duplicate key value violates unique constraint",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: sqlState,
            tableName: tableName,
            constraintName: constraintName);
}
