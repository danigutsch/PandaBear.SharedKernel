using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PandaBear.Domain.EntityFrameworkCore.Tests;

internal sealed class FailOnceSaveInterceptor : SaveChangesInterceptor
{
    private bool failed;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (failed)
        {
            return ValueTask.FromResult(result);
        }

        failed = true;
        throw new InvalidOperationException("save failed");
    }
}
