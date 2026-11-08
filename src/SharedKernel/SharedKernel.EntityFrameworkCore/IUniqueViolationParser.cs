using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore;

/// <summary>Recognizes unique-index violations of one database provider. Implementations hold all provider-specific knowledge.</summary>
public interface IUniqueViolationParser
{
    /// <summary>Tries to read a unique-index violation from a failed save.</summary>
    /// <param name="exception">The exception thrown by the save.</param>
    /// <param name="violation">The violation details when the exception is a unique-index violation.</param>
    /// <returns><see langword="true"/> when the exception is a unique-index violation.</returns>
    bool TryParse(DbUpdateException exception, out UniqueViolation violation);
}
