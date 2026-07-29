using Microsoft.EntityFrameworkCore;
using SharedKernel.Testing;

namespace SharedKernel.EntityFrameworkCore.Tests;

[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.DatabaseIntegrationCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.PostgreSqlFixtureCapability)]
public sealed class PostgreSqlFixtureTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Concurrent_requests_create_isolated_databases()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var firstDatabaseTask = fixture.CreateIsolatedDatabase(ct);
        var secondDatabaseTask = fixture.CreateIsolatedDatabase(ct);
        await Task.WhenAll(firstDatabaseTask, secondDatabaseTask);
        await using var firstDatabase = await firstDatabaseTask;
        await using var secondDatabase = await secondDatabaseTask;
        await using var firstDataSource = firstDatabase.CreateDataSource("first");
        await using var secondDataSource = secondDatabase.CreateDataSource("second");
        var firstOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(firstDataSource)
            .Options;
        var secondOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(secondDataSource)
            .Options;
        await using var firstContext = new TestDbContext(firstOptions);
        await using var secondContext = new TestDbContext(secondOptions);
        _ = await firstContext.Database.EnsureCreatedAsync(ct);
        _ = await secondContext.Database.EnsureCreatedAsync(ct);

        // Act
        firstContext.Entities.Add(new TestEntity { Name = "first" });
        _ = await firstContext.SaveChangesAsync(ct);
        var firstCount = await firstContext.Entities.CountAsync(ct);
        var secondCount = await secondContext.Entities.CountAsync(ct);

        // Assert
        firstCount.ShouldBe(1);
        secondCount.ShouldBe(0);
    }

    [Fact]
    public async Task Cancelled_database_creation_removes_the_registered_name()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var existingDatabase = await fixture.CreateIsolatedDatabase(ct);
        var databaseName = $"{PostgreSqlTestDatabase.DatabaseNamePrefix}{Guid.NewGuid():N}";
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> createDatabase = async () =>
            await fixture.CreateIsolatedDatabase(databaseName, cancellation.Token);
        var exception = await createDatabase.ShouldThrowAssignableTo<OperationCanceledException>();
        var ownsDatabase = fixture.OwnsDatabase(databaseName);

        // Assert
        exception.ShouldNotBeNull();
        ownsDatabase.ShouldBeFalse();
    }

    [Fact]
    public async Task Disposing_a_database_forces_cleanup_with_an_open_connection()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var database = await fixture.CreateIsolatedDatabase(ct);
        await using var dataSource = database.CreateDataSource("open-connection");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var databaseName = database.DatabaseName;

        // Act
        await database.DisposeAsync();
        var databaseExists = await fixture.DatabaseExists(databaseName, ct);

        // Assert
        databaseExists.ShouldBeFalse();
    }

    [Fact]
    public async Task Cleanup_rejects_a_database_name_not_issued_by_the_fixture()
    {
        // Arrange
        Func<Task> dropDatabase = () => PostgreSqlTestDatabase.DropDatabase(
            "Host=localhost;Database=postgres;Username=test;Password=test",
            "postgres");

        // Act
        var exception = await dropDatabase.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("databaseName");
    }
}
