using PandaBear.Idempotency;

namespace PandaBear.EntityFrameworkCore.Tests;

internal static class IdempotencyOperationTestFactory
{
    public static IdempotencyOperation Create() =>
        new(
            IdempotencyScope.From("integration-event:test.created"),
            IdempotencyKey.From(Guid.CreateVersion7().ToString("N")));
}
