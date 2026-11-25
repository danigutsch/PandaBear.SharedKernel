using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>A parser that never recognizes a violation, standing in for an application's own registration.</summary>
internal sealed class FixedTestParser : IUniqueViolationParser
{
    /// <inheritdoc />
    public bool TryParse(DbUpdateException exception, out UniqueViolation violation)
    {
        violation = new UniqueViolation(null, null, []);
        return false;
    }
}
