using Microsoft.EntityFrameworkCore;
using SharedKernel.Testing;

namespace SharedKernel.EntityFrameworkCore.Tests;

public sealed class EfCoreCommandTransactionScopeTests : IAsyncLifetime
{
    private const string PostgreSqlResourceName = "postgres";
    private const string DatabaseResourceName = "efcoretransactions";

    private AspireTestApplication? app;
    private string? connectionString;

    public async ValueTask InitializeAsync()
    {
        var appBuilder = DistributedApplication.CreateBuilder([]);
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

        Assert.Equal(42, response);
        Assert.True(persisted);
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

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await EfCoreCommandTransactionScope.Execute<int>(
                dbContext,
                async () =>
                {
                    dbContext.Entities.Add(new TestEntity { Name = "rolled-back" });
                    await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                    throw new InvalidOperationException("boom");
                },
                TestContext.Current.CancellationToken));

        // Assert
        var persisted = await dbContext.Entities.AnyAsync(
            entity => entity.Name == "rolled-back",
            TestContext.Current.CancellationToken);

        Assert.False(persisted);
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

        Assert.Equal(24, response);
        Assert.NotNull(activeTransaction);
        Assert.False(persisted);
    }

    [Fact]
    public async Task Runs_without_a_transaction_for_non_relational_contexts()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var dbContext = new TestDbContext(options);

        // Act
        var response = await EfCoreCommandTransactionScope.Execute(
            dbContext,
            () => ValueTask.FromResult(12),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(12, response);
    }

    [Fact]
    public async Task Rejects_missing_dbcontext()
    {
        // Arrange
        TestDbContext? dbContext = null;

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await EfCoreCommandTransactionScope.Execute(dbContext!, () => ValueTask.FromResult(1), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("dbContext", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_missing_next_handler()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var dbContext = new TestDbContext(options);
        Func<ValueTask<int>>? next = null;

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await EfCoreCommandTransactionScope.Execute(dbContext, next!, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("next", exception.ParamName);
    }
}
