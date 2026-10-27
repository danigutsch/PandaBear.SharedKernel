namespace PandaBear.Npgsql.Tests;

/// <summary>A cleanup action that records its name in a shared list when invoked, optionally returning a faulted task.</summary>
/// <param name="name">The name appended to the cleanup list on each invocation.</param>
/// <param name="cleanupOrder">The shared list that records the order in which cleanups ran.</param>
/// <param name="failure">The exception returned as a faulted task, or null to complete successfully.</param>
internal sealed class TrackingCleanupAction(
    string name,
    ICollection<string> cleanupOrder,
    Exception? failure = null)
{
    /// <summary>Records the cleanup name and returns the configured failure as a faulted task, or a completed task when there is none.</summary>
    internal ValueTask Invoke()
    {
        cleanupOrder.Add(name);
        return failure is null
            ? ValueTask.CompletedTask
            : ValueTask.FromException(failure);
    }
}
