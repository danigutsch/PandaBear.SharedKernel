using Microsoft.EntityFrameworkCore;
using SharedKernel.IntegrationTesting;

namespace SharedKernel.EntityFrameworkCore.Tests;

public sealed class EfCoreCommandTransactionScopeTests : IAsyncLifetime
{
    private const string PostgreSqlResourceName = "postgres";
    private const string DatabaseResourceName = "efcoretransactions";

    private AspireTestApplication? app;
    private string? connectionString;

    public async ValueTask InitializeAsync()
    {
        var appBuilder = AspireTestApplication.CreateBuilder();
        var databaseServer = appBuilder.AddPostgres(PostgreSqlResourceName);
        _ = databaseServer.AddDatabase(DatabaseResourceName);

        app = await AspireTestApplication.Start(appBuilder, [PostgreSqlResourceName], null, TestContext.Current.CancellationToken);
        connectionString = await app.GetConnectionString(DatabaseResourceName, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var application = app;
        app = null;
        connectionString = null;

        if (application is not null)
        {
            await application.DisposeAsync();
        }
    }

    [Fact]
    public async Task Commits_successful_work_inside_a_postgres_transaction()
    {
        // Arrange
        var currentConnectionString = connectionString ?? throw new InvalidOperationException("PostgreSQL is not started.");
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(currentConnectionString)
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
        var currentConnectionString = connectionString ?? throw new InvalidOperationException("PostgreSQL is not started.");
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(currentConnectionString)
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
        var currentConnectionString = connectionString ?? throw new InvalidOperationException("PostgreSQL is not started.");
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(currentConnectionString)
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
