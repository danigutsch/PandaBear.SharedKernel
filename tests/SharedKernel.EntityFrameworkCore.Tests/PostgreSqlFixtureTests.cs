using Microsoft.EntityFrameworkCore;
using PandaBear.Testing;

namespace PandaBear.EntityFrameworkCore.Tests;

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

    // A token that is already cancelled stops the pool at its gate, before it registers a name, so this test cannot show
    // the release of a name that was registered first. SharedKernel.Npgsql.Tests covers that path on the same lease and
    // pool code: PostgreSqlTestDatabaseLeaseCreationTests registers a name itself and then cancels, and
    // PostgreSqlTestDatabasePoolCancellationTests cancels after the pool has resolved the server. The pool is private to
    // this test because the fixture's pool is shared with tests that run in parallel, so only a private pool on the
    // fixture's server lets the test compare the registered names exactly.
    [Fact]
    public async Task Cancelled_database_creation_removes_the_registered_name()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var pool = new PostgreSqlTestDatabasePool(await fixture.GetServerConnectionString(ct));
        await using var existingDatabase = await pool.Create(ct);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> createDatabase = async () => await pool.Create(cancellation.Token);
        var exception = await createDatabase.ShouldThrowAssignableTo<OperationCanceledException>();
        var ownedNames = pool.OwnedDatabaseNames;
        var existingDatabaseExists = await fixture.DatabaseExists(existingDatabase.DatabaseName, ct);
        await using var nextDatabase = await pool.Create(ct);
        var nextDatabaseExists = await fixture.DatabaseExists(nextDatabase.DatabaseName, ct);

        // Assert
        exception.ShouldNotBeNull();
        ownedNames.ShouldHaveSingleItem().ShouldBe(existingDatabase.DatabaseName);
        existingDatabaseExists.ShouldBeTrue();
        nextDatabaseExists.ShouldBeTrue();
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
        Func<Task> dropDatabase = () => PostgreSqlTestDatabaseLease.DropDatabase(
            "Host=localhost;Database=postgres;Username=test;Password=test",
            "postgres");

        // Act
        var exception = await dropDatabase.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("databaseName");
    }
}
