using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Tracks the DbContext currently executing a SaveChanges pipeline on the async control flow.
/// </summary>
public static class CurrentSaveChangesDbContext
{
    private static readonly AsyncLocal<DbContext?> CurrentContext = new();

    /// <summary>
    /// Gets the DbContext currently executing a SaveChanges pipeline, when one exists.
    /// </summary>
    public static DbContext? Current => CurrentContext.Value;

    /// <summary>
    /// Sets the current SaveChanges DbContext for the lifetime of the returned scope.
    /// </summary>
    /// <param name="dbContext">The current DbContext.</param>
    /// <returns>A scope that restores the previous DbContext when disposed.</returns>
    public static IDisposable Enter(DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var previous = CurrentContext.Value;
        CurrentContext.Value = dbContext;

        return new RestoreCurrentDbContextScope(previous);
    }

    private sealed class RestoreCurrentDbContextScope(DbContext? previous) : IDisposable
    {
        public void Dispose()
        {
            CurrentContext.Value = previous;
        }
    }
}
