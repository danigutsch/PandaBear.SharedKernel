using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A parser that reports a fixed violation for every exception, to test rule matching in isolation.</summary>
/// <param name="reported">The violation to report.</param>
public sealed class FixedUniqueViolationParser(UniqueViolation reported) : IUniqueViolationParser
{
    /// <inheritdoc />
    public bool TryParse(DbUpdateException exception, out UniqueViolation violation)
    {
        violation = reported;
        return true;
    }
}
