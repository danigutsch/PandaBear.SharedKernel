namespace SharedKernel.Npgsql.Tests;

internal sealed class TrackingCleanupAction(
    string name,
    ICollection<string> cleanupOrder,
    Exception? failure = null)
{
    internal ValueTask Invoke()
    {
        cleanupOrder.Add(name);
        return failure is null
            ? ValueTask.CompletedTask
            : ValueTask.FromException(failure);
    }
}
