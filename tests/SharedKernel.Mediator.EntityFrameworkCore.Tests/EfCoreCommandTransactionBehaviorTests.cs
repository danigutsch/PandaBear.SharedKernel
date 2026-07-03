using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Mediator.EntityFrameworkCore.Tests;

public sealed class EfCoreCommandTransactionBehaviorTests
{
    [Fact]
    public async Task Commands_run_the_next_handler()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var dbContext = new TestDbContext(options);
        var behavior = new TestCommandTransactionBehavior<TestCommand, int>(dbContext);
        var callCount = 0;

        // Act
        var response = await behavior.Handle(
            new TestCommand(),
            () =>
            {
                callCount++;
                return ValueTask.FromResult(42);
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(42, response);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Queries_bypass_the_transaction_path()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var dbContext = new TestDbContext(options);
        await dbContext.DisposeAsync();
        var behavior = new TestCommandTransactionBehavior<TestQuery, int>(dbContext);

        // Act
        var response = await behavior.Handle(
            new TestQuery(),
            () => ValueTask.FromResult(42),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(42, response);
    }

    [Fact]
    public void Registers_the_selected_dbcontext_as_the_transaction_boundary()
    {
        // Arrange
        using var scope = new EfCoreMediatorTestScope();

        // Act
        var dbContext = scope.DbContext;
        var registeredBoundary = scope.TransactionBoundary;
        var behavior = scope.CommandBehavior;

        // Assert
        Assert.Same(dbContext, registeredBoundary);
        Assert.IsType<EfCoreCommandTransactionBehavior<TestCommand, int>>(behavior);
    }

    [Fact]
    public async Task Rejects_missing_request()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var dbContext = new TestDbContext(options);
        var behavior = new TestCommandTransactionBehavior<TestCommand, int>(dbContext);
        TestCommand? command = null;

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await behavior.Handle(command!, () => ValueTask.FromResult(1), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_missing_next_handler()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var dbContext = new TestDbContext(options);
        var behavior = new TestCommandTransactionBehavior<TestCommand, int>(dbContext);
        RequestHandlerContinuation<int>? next = null;

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await behavior.Handle(new TestCommand(), next!, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("next", exception.ParamName);
    }
}
