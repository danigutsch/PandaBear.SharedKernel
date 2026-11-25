namespace PandaBear.DispositionHold;

/// <summary>Composes disposition hold queries without choosing the caller's scope.</summary>
public static class HoldQueryableExtensions
{
    /// <summary>Filters holds to the unreleased rows effective at the given time and before any expiry.</summary>
    /// <param name="holds">The caller's query for holds in scope.</param>
    /// <param name="at">The time to evaluate.</param>
    /// <returns>A query with a server-translatable restraint filter.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="holds"/> is null.</exception>
    public static IQueryable<Hold> WhereRestrainingAt(this IQueryable<Hold> holds, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(holds);
        return holds.Where(hold => hold.ReleasedAt == null && hold.EffectiveFrom <= at
            && (hold.ExpiresAt == null || at < hold.ExpiresAt));
    }
}
