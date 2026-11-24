namespace PandaBear.Idempotency.Tests;

/// <summary>Verifies how an existing strict-only store behaves through the additive retention contract.</summary>
public sealed class IIdempotencyRetentionStoreCutoffTests
{
    [Fact]
    public async Task Existing_store_keeps_strict_purge_and_explicitly_rejects_inclusive_purge()
    {
        // Arrange
        IIdempotencyRetentionStore store = new StrictIdempotencyRetentionStore();
        var ct = TestContext.Current.CancellationToken;
        var cutoff = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
        Func<Task> inclusive = async () => await store.PurgeExpiredThrough(cutoff, null, [], 10, ct);

        // Act
        var strict = await store.PurgeExpired(cutoff, null, [], 10, ct);
        var exception = await inclusive.ShouldThrow<NotSupportedException>();

        // Assert
        strict.Purged.ShouldBe(1L);
        exception.Message.ShouldContain("inclusive retention cutoff", StringComparison.Ordinal);
    }
}
