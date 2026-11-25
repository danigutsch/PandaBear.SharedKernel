using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SharedKernel.EntityFrameworkCore.Npgsql;

/// <summary>
/// Recognizes PostgreSQL unique and primary-key violations: a <see cref="PostgresException"/> with SQLSTATE <c>23505</c>
/// (<see cref="PostgresErrorCodes.UniqueViolation"/>) anywhere in the inner-exception chain. PostgreSQL names the violated constraint, which
/// is the index name for a unique index, and its table, so those are what it reports; the columns are only in the error detail, which
/// Npgsql hides unless <c>Include Error Detail</c> is set, so they are not read.
/// </summary>
public sealed class NpgsqlUniqueViolationParser : IUniqueViolationParser
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    public bool TryParse(DbUpdateException exception, out UniqueViolation violation)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: { Length: > 0 } constraint } postgres)
            {
                violation = new UniqueViolation(constraint, postgres.TableName, []);
                return true;
            }
        }

        violation = new UniqueViolation(null, null, []);
        return false;
    }
}
