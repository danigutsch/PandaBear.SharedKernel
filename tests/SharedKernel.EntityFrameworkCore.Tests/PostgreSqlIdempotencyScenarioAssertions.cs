using PandaBear.Idempotency;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Assertions over the entries stored by a PostgreSQL idempotency scenario.
/// </summary>
internal static class PostgreSqlIdempotencyScenarioAssertions
{
    /// <summary>
    /// Asserts that the entry of the operation no longer exists.
    /// </summary>
    public static async Task ShouldHaveDeleted(
        this PostgreSqlIdempotencyStoreScenario scenario,
        IdempotencyOperation operation,
        CancellationToken ct)
    {
        var entry = await scenario.Find(operation, ct);
        entry.ShouldBeNull();
    }

    /// <summary>
    /// Asserts that the entry of the operation still exists.
    /// </summary>
    public static async Task ShouldHaveRetained(
        this PostgreSqlIdempotencyStoreScenario scenario,
        IdempotencyOperation operation,
        CancellationToken ct)
    {
        var entry = await scenario.Find(operation, ct);
        entry.ShouldNotBeNull();
    }

    /// <summary>
    /// Asserts the number of stored entries.
    /// </summary>
    public static async Task ShouldHaveEntryCount(
        this PostgreSqlIdempotencyStoreScenario scenario,
        int expected,
        CancellationToken ct)
    {
        var count = await scenario.CountEntries(ct);
        count.ShouldBe(expected);
    }
}
