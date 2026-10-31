using PandaBear.Idempotency.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Tests that EfIdempotencyStore records and returns the retention owner and that the retention store validates its input.
/// </summary>
[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IdempotencyCapability)]
public sealed class EfIdempotencyStoreRetentionOwnerTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TryStart_with_retention_owner_stores_the_owner_and_get_returns_it()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();

        // Act
        var result = await store.TryStart(operation, StartedAt, TimeSpan.FromMinutes(5), "document-7", CancellationToken.None);
        var entry = await store.Get(operation, CancellationToken.None);

        // Assert
        result.Started.ShouldBeTrue();
        entry.ShouldNotBeNull().RetentionOwner.ShouldBe("document-7");
    }

    [Fact]
    public async Task TryStart_without_retention_owner_leaves_the_owner_empty()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();

        // Act
        await store.TryStart(operation, StartedAt, TimeSpan.FromMinutes(5), CancellationToken.None);
        var entry = await store.Get(operation, CancellationToken.None);

        // Assert
        entry.ShouldNotBeNull().RetentionOwner.ShouldBeNull();
    }

    [Fact]
    public async Task TryStart_with_null_retention_owner_behaves_like_the_overload_without_owner()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();

        // Act
        var result = await store.TryStart(operation, StartedAt, TimeSpan.FromMinutes(5), retentionOwner: null, CancellationToken.None);
        var entry = await store.Get(operation, CancellationToken.None);

        // Assert
        result.Started.ShouldBeTrue();
        entry.ShouldNotBeNull().RetentionOwner.ShouldBeNull();
    }

    [Fact]
    public async Task TryStart_duplicate_reports_the_stored_owner_and_does_not_replace_it()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();
        await store.TryStart(operation, StartedAt, TimeSpan.FromMinutes(5), "document-7", CancellationToken.None);

        // Act
        var duplicate = await store.TryStart(operation, StartedAt.AddMinutes(1), TimeSpan.FromMinutes(5), "document-8", CancellationToken.None);

        // Assert
        duplicate.Started.ShouldBeFalse();
        duplicate.ExistingEntry.ShouldNotBeNull().RetentionOwner.ShouldBe("document-7");
    }

    [Fact]
    public async Task TryStart_restart_with_owner_records_the_new_owner()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();
        await store.TryStart(operation, StartedAt, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Act
        var restarted = await store.TryStart(operation, StartedAt.AddMinutes(6), TimeSpan.FromMinutes(5), "document-7", CancellationToken.None);
        var entry = await store.Get(operation, CancellationToken.None);

        // Assert
        restarted.Started.ShouldBeTrue();
        entry.ShouldNotBeNull().RetentionOwner.ShouldBe("document-7");
    }

    [Fact]
    public async Task TryStart_restart_without_owner_keeps_the_stored_owner()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();
        await store.TryStart(operation, StartedAt, TimeSpan.FromMinutes(5), "document-7", CancellationToken.None);

        // Act
        await store.TryStart(operation, StartedAt.AddMinutes(6), TimeSpan.FromMinutes(5), CancellationToken.None);
        var entry = await store.Get(operation, CancellationToken.None);

        // Assert
        entry.ShouldNotBeNull().RetentionOwner.ShouldBe("document-7");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("jane doe")]
    [InlineData("jane@example.com")]
    [InlineData("café")]
    public async Task TryStart_rejects_a_retention_owner_with_forbidden_characters(string retentionOwner)
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();

        // Act, Assert
        await ShouldAssertionExtensions.ShouldThrow<ArgumentException>((Func<Task>)(async () =>
        {
            await store.TryStart(operation, StartedAt, TimeSpan.FromMinutes(5), retentionOwner, CancellationToken.None);
        }));
        var entry = await store.Get(operation, CancellationToken.None);
        entry.ShouldBeNull();
    }

    [Fact]
    public async Task TryStart_accepts_a_retention_owner_of_the_maximum_length_and_rejects_one_longer()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var store = new EfIdempotencyStore<ConsumerIdempotencyDbContext>(dbContext);
        var operation = IdempotencyOperationTestFactory.Create();

        // Act
        var started = await store.TryStart(operation, StartedAt, null, new string('a', 128), CancellationToken.None);

        // Assert
        started.Started.ShouldBeTrue();
        await ShouldAssertionExtensions.ShouldThrow<ArgumentException>((Func<Task>)(async () =>
        {
            await store.TryStart(IdempotencyOperationTestFactory.Create(), StartedAt, null, new string('a', 129), CancellationToken.None);
        }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task PurgeExpired_rejects_a_batch_size_that_is_not_positive(int batchSize)
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var retention = new EfIdempotencyRetentionStore<ConsumerIdempotencyDbContext>(dbContext);

        // Act, Assert
        await ShouldAssertionExtensions.ShouldThrow<ArgumentOutOfRangeException>((Func<Task>)(async () =>
        {
            await retention.PurgeExpired(StartedAt, null, [], batchSize, CancellationToken.None);
        }));
    }

    [Fact]
    public async Task PurgeExpired_rejects_a_held_owner_with_forbidden_characters()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var retention = new EfIdempotencyRetentionStore<ConsumerIdempotencyDbContext>(dbContext);

        // Act, Assert
        await ShouldAssertionExtensions.ShouldThrow<ArgumentException>((Func<Task>)(async () =>
        {
            await retention.PurgeExpired(StartedAt, null, ["jane doe"], 10, CancellationToken.None);
        }));
    }

    [Fact]
    public async Task PurgeExpired_requires_the_postgresql_provider()
    {
        // Arrange
        await using var dbContext = ConsumerIdempotencyTestServices.Create();
        var retention = new EfIdempotencyRetentionStore<ConsumerIdempotencyDbContext>(dbContext);

        // Act, Assert
        await ShouldAssertionExtensions.ShouldThrow<InvalidOperationException>((Func<Task>)(async () =>
        {
            await retention.PurgeExpired(StartedAt, null, [], 10, CancellationToken.None);
        }));
    }
}
