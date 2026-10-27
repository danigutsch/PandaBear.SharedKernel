using PandaBear.Idempotency;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Creates idempotency operations for tests.
/// </summary>
internal static class IdempotencyOperationTestFactory
{
    /// <summary>
    /// Creates an operation in the scope "integration-event:test.created" with a new random key.
    /// </summary>
    public static IdempotencyOperation Create() =>
        new(
            IdempotencyScope.From("integration-event:test.created"),
            IdempotencyKey.From(Guid.CreateVersion7().ToString("N")));
}
