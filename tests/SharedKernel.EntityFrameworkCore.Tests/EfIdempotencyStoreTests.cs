using PandaBear.Idempotency;
using PandaBear.Testing;
using PandaBear.Idempotency.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IdempotencyCapability)]
public sealed class EfIdempotencyStoreTests
{
    [Fact]
    public async Task TryStart_creates_durable_started_entry()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();
        var startedAt = new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero);

        // Act
        var result = await store.TryStart(operation, startedAt, TimeSpan.FromMinutes(5), CancellationToken.None);
        var actualResult = await store.Get(operation, CancellationToken.None);

        // Assert
        result.Started.ShouldBeTrue();
        var entry = actualResult.ShouldNotBeNull();
        entry.State.ShouldBe(IdempotencyEntryState.Started);
        entry.StartedAt.ShouldBe(startedAt);
    }

    [Fact]
    public async Task TryStart_treats_completed_entry_as_safe_duplicate()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();
        var startedAt = new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero);
        await store.TryStart(operation, startedAt, TimeSpan.FromMinutes(5), CancellationToken.None);
        await store.Complete(operation, startedAt.AddSeconds(10), resultFingerprint: null, CancellationToken.None);

        // Act
        var duplicate = await store.TryStart(
            operation,
            startedAt.AddMinutes(10),
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        // Assert
        duplicate.Started.ShouldBeFalse();
        duplicate.ExistingEntry.ShouldNotBeNull().State.ShouldBe(IdempotencyEntryState.Completed);
    }

    [Fact]
    public async Task TryStart_restarts_expired_started_entry()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();
        var startedAt = new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero);
        var restartedAt = startedAt.AddMinutes(6);
        await store.TryStart(operation, startedAt, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Act
        var restarted = await store.TryStart(operation, restartedAt, TimeSpan.FromMinutes(5), CancellationToken.None);
        var actualResult = await store.Get(operation, CancellationToken.None);

        // Assert
        restarted.Started.ShouldBeTrue();
        var entry = actualResult.ShouldNotBeNull();
        entry.State.ShouldBe(IdempotencyEntryState.Started);
        entry.StartedAt.ShouldBe(restartedAt);
    }

    [Fact]
    public async Task TryStart_treats_started_entry_without_lock_duration_as_duplicate()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();
        var startedAt = new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero);
        await store.TryStart(operation, startedAt, lockDuration: null, CancellationToken.None);

        // Act
        var duplicate = await store.TryStart(
            operation,
            startedAt.AddHours(1),
            lockDuration: null,
            CancellationToken.None);

        // Assert
        duplicate.Started.ShouldBeFalse();
        var existing = duplicate.ExistingEntry.ShouldNotBeNull();
        existing.State.ShouldBe(IdempotencyEntryState.Started);
        existing.StartedAt.ShouldBe(startedAt);
    }

    [Fact]
    public async Task TryStart_treats_unexpired_started_entry_as_duplicate()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();
        var startedAt = new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero);
        await store.TryStart(operation, startedAt, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Act
        var duplicate = await store.TryStart(
            operation,
            startedAt.AddMinutes(4),
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        // Assert
        duplicate.Started.ShouldBeFalse();
        var existing = duplicate.ExistingEntry.ShouldNotBeNull();
        existing.State.ShouldBe(IdempotencyEntryState.Started);
        existing.StartedAt.ShouldBe(startedAt);
    }

    [Fact]
    public async Task Complete_persists_completion_time_and_result_fingerprint()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();
        var startedAt = new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero);
        var completedAt = startedAt.AddSeconds(30);
        await store.TryStart(operation, startedAt, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Act
        await store.Complete(operation, completedAt, "tour-42", CancellationToken.None);
        var actualResult = await store.Get(operation, CancellationToken.None);

        // Assert
        var entry = actualResult.ShouldNotBeNull();
        entry.State.ShouldBe(IdempotencyEntryState.Completed);
        entry.CompletedAt.ShouldBe(completedAt);
        entry.ResultFingerprint.ShouldBe("tour-42");
    }

    [Fact]
    public async Task Complete_rejects_operation_that_was_not_started()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();

        Func<Task> complete = () => store
            .Complete(operation, TimeProvider.System.GetUtcNow(), resultFingerprint: null, CancellationToken.None)
            .AsTask();

        // Act
        var exception = await complete.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("must be started", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_returns_null_when_entry_does_not_exist()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();

        // Act
        var entry = await store.Get(operation, CancellationToken.None);

        // Assert
        entry.ShouldBeNull();
    }

    [Theory]
    [InlineData("", "key", "scope")]
    [InlineData("   ", "key", "scope")]
    [InlineData("scope", "", "key")]
    [InlineData("scope", "   ", "key")]
    public void Idempotency_entry_rejects_blank_identity_parts(string scope, string key, string parameterName)
    {
        // Arrange
        Action create = () =>
        {
            _ = new IdempotencyEntryEntity(scope, key, TimeProvider.System.GetUtcNow());
        };

        // Act
        var exception = create.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe(parameterName);
    }

    [Theory]
    [InlineData(null, "key", "scope")]
    [InlineData("scope", null, "key")]
    public void Idempotency_entry_rejects_null_identity_parts(string? scope, string? key, string parameterName)
    {
        // Arrange
        Action create = () =>
        {
            _ = new IdempotencyEntryEntity(scope!, key!, TimeProvider.System.GetUtcNow());
        };

        // Act
        var exception = create.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void Idempotency_entry_restart_clears_completion_state()
    {
        // Arrange
        var entry = new IdempotencyEntryEntity(
            "integration-event:test.created",
            Guid.CreateVersion7().ToString("N"),
            new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero));
        entry.Complete(new DateTimeOffset(2026, 6, 22, 12, 1, 0, TimeSpan.Zero), "tour-42");

        // Act
        entry.Restart(new DateTimeOffset(2026, 6, 22, 12, 10, 0, TimeSpan.Zero));

        // Assert
        entry.State.ShouldBe(IdempotencyEntryState.Started);
        entry.CompletedAt.ShouldBeNull();
        entry.ResultFingerprint.ShouldBeNull();
        entry.StartedAt.ShouldBe(new DateTimeOffset(2026, 6, 22, 12, 10, 0, TimeSpan.Zero));
    }
}
