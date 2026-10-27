using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Save interceptor that holds every save until released so two concurrent saves can be forced to overlap.
/// </summary>
internal sealed class ConcurrentSaveBarrierInterceptor : SaveChangesInterceptor
{
    private readonly TaskCompletionSource bothSaving = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int arrivals;

    /// <summary>
    /// Task that completes once two saves have reached the interceptor.
    /// </summary>
    public Task BothSaving => bothSaving.Task;

    /// <summary>
    /// Lets the held saves continue.
    /// </summary>
    public void Release() => release.TrySetResult();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref arrivals) == 2)
        {
            bothSaving.TrySetResult();
        }

        await release.Task.WaitAsync(cancellationToken);
        return result;
    }
}
