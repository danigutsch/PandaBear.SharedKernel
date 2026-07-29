using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SharedKernel.Testing;

namespace SharedKernel.EntityFrameworkCore.Tests;

[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.DatabaseIntegrationCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.CommandTransactionScopeCapability)]
[SuppressMessage(
    "Usage",
    "CA2213:Disposable fields should be disposed",
    Justification = "DisposeAsync passes the data source to the independent aggregate cleanup helper.")]
public sealed class EfCoreCommandTransactionScopeTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private PostgreSqlTestDatabase? database;
    private NpgsqlDataSource? dataSource;

    private NpgsqlDataSource DataSource =>
        dataSource ?? throw new InvalidOperationException("PostgreSQL test database is not initialized.");

    public async ValueTask InitializeAsync()
    {
        database = await fixture.CreateIsolatedDatabase(TestContext.Current.CancellationToken);
        dataSource = database.CreateDataSource(nameof(EfCoreCommandTransactionScopeTests));
    }

    public async ValueTask DisposeAsync()
    {
        var currentDataSource = dataSource;
        var currentDatabase = database;
        dataSource = null;
        database = null;

        await PostgreSqlScenarioCleanup.DisposeResources(null, currentDataSource, currentDatabase);
    }

    [Fact]
    public async Task Commits_successful_work_inside_a_postgres_transaction()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(DataSource)
            .Options;
        await using var dbContext = new TestDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await EfCoreCommandTransactionScope.Execute(
            dbContext,
            async () =>
            {
                dbContext.Entities.Add(new TestEntity { Name = "committed" });
                await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                return 42;
            },
            TestContext.Current.CancellationToken);

        // Assert
        var persisted = await dbContext.Entities.AnyAsync(
            entity => entity.Name == "committed",
            TestContext.Current.CancellationToken);

        response.ShouldBe(42);
        persisted.ShouldBeTrue();
    }

    [Fact]
    public async Task Rolls_back_failed_work_inside_a_postgres_transaction()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(DataSource)
            .Options;
        await using var dbContext = new TestDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Func<Task> execute = async () =>
            await EfCoreCommandTransactionScope.Execute<int>(
                dbContext,
                async () =>
                {
                    dbContext.Entities.Add(new TestEntity { Name = "rolled-back" });
                    await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                    throw new InvalidOperationException("boom");
                },
                TestContext.Current.CancellationToken);

        // Act
        await execute.ShouldThrow<InvalidOperationException>();

        // Assert
        var persisted = await dbContext.Entities.AnyAsync(
            entity => entity.Name == "rolled-back",
            TestContext.Current.CancellationToken);

        persisted.ShouldBeFalse();
    }

    [Fact]
    public async Task Uses_the_existing_transaction_without_committing_it()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(DataSource)
            .Options;
        await using var dbContext = new TestDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var transaction = await dbContext.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var _ = transaction.ConfigureAwait(false);

        // Act
        var response = await EfCoreCommandTransactionScope.Execute(
            dbContext,
            async () =>
            {
                dbContext.Entities.Add(new TestEntity { Name = "outer-transaction" });
                await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                return 24;
            },
            TestContext.Current.CancellationToken);
        var activeTransaction = dbContext.Database.CurrentTransaction;
        await transaction.RollbackAsync(TestContext.Current.CancellationToken);

        // Assert
        var persisted = await dbContext.Entities.AnyAsync(
            entity => entity.Name == "outer-transaction",
            TestContext.Current.CancellationToken);

        response.ShouldBe(24);
        activeTransaction.ShouldNotBeNull();
        persisted.ShouldBeFalse();
    }

}
