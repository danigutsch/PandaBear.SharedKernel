using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Pauses exactly one save after it has loaded its concurrency version.</summary>
internal sealed class DocumentWorkflowSaveGate : SaveChangesInterceptor
{
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int paused;

    /// <summary>Gets completion when the paused writer reaches SaveChanges.</summary>
    public Task Entered => entered.Task;

    /// <summary>Pauses the next write.</summary>
    public void Arm() => Interlocked.Exchange(ref paused, 1);

    /// <summary>Releases the paused write.</summary>
    public void Release() => released.TrySetResult();

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref paused, 0) == 1)
        {
            entered.TrySetResult();
            await released.Task.WaitAsync(cancellationToken);
        }

        return result;
    }
}
