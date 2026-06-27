using System.Globalization;

namespace SharedKernel.Idempotency.Tests;

public sealed class IdempotencyStartResultTests
{
    [Fact]
    public void StartedNew_creates_ownership_result()
    {
        // Arrange, Act
        var result = IdempotencyStartResult.StartedNew();

        // Assert
        Assert.True(result.Started);
        Assert.Null(result.ExistingEntry);
    }

    [Fact]
    public void AlreadyStarted_carries_existing_entry()
    {
        // Arrange
        var operation = new IdempotencyOperation(
            IdempotencyScope.From("request:create-booking"),
            IdempotencyKey.From("request-123"));
        var entry = new IdempotencyEntry(
            operation,
            IdempotencyEntryState.Completed,
            DateTimeOffset.Parse("2026-06-21T10:00:00Z", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-06-21T10:00:01Z", CultureInfo.InvariantCulture),
            "sha256:result");

        // Act
        var result = IdempotencyStartResult.AlreadyStarted(entry);

        // Assert
        Assert.False(result.Started);
        Assert.Same(entry, result.ExistingEntry);
    }

    [Fact]
    public void AlreadyStarted_rejects_null_entry()
    {
        // Arrange
        dynamic? entry = null;

        // Act, Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = IdempotencyStartResult.AlreadyStarted(entry);
        });
    }
}
