using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        response.ShouldBe(42);
        callCount.ShouldBe(1);
    }

    [Fact]
    public async Task Queries_bypass_the_transaction_path()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var dbContext = new TestDbContext(options);
        await dbContext.DisposeAsync();
        var behavior = new TestCommandTransactionBehavior<TestQuery, int>(dbContext);

        // Act
        var response = await behavior.Handle(
            new TestQuery(),
            () => ValueTask.FromResult(42),
            TestContext.Current.CancellationToken);

        // Assert
        response.ShouldBe(42);
    }

    [Fact]
    public async Task Commands_use_the_transaction_path_before_running_the_next_handler()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var dbContext = new TestDbContext(options);
        await dbContext.DisposeAsync();
        var behavior = new TestCommandTransactionBehavior<TestCommand, int>(dbContext);
        Func<Task> handle = async () =>
            await behavior.Handle(
                new TestCommand(),
                () => ValueTask.FromResult(42),
                TestContext.Current.CancellationToken);

        // Act
        var exception = await handle.ShouldThrow<ObjectDisposedException>();

        // Assert
        exception.ObjectName.ShouldBe(nameof(TestDbContext));
    }

    [Fact]
    public async Task Commands_without_response_use_the_transaction_path_before_running_the_next_handler()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var dbContext = new TestDbContext(options);
        await dbContext.DisposeAsync();
        var behavior = new TestCommandTransactionBehavior<NonGenericTestCommand, Unit>(dbContext);
        Func<Task> handle = async () =>
            await behavior.Handle(
                new NonGenericTestCommand(),
                () => ValueTask.FromResult(Unit.Value),
                TestContext.Current.CancellationToken);

        // Act
        var exception = await handle.ShouldThrow<ObjectDisposedException>();

        // Assert
        exception.ObjectName.ShouldBe(nameof(TestDbContext));
    }

    [Fact]
    public void Registers_closed_transaction_behavior_without_global_dbcontext()
    {
        // Arrange
        using var scope = new EfCoreMediatorTestScope();

        // Act
        var behavior = scope.CommandBehavior;
        Action resolveGlobalDbContext = () => scope.ResolveGlobalDbContext();

        // Assert
        resolveGlobalDbContext.ShouldThrow<InvalidOperationException>();
        behavior.ShouldBeOfType<EfCoreCommandTransactionBehavior<TestCommand, int>>();
    }

    [Fact]
    public void Registers_closed_transaction_behaviors_for_multiple_dbcontexts()
    {
        // Arrange
        using var scope = new EfCoreMediatorTestScope();

        // Act
        var commandBehavior = scope.CommandBehavior;
        var otherCommandBehavior = scope.OtherCommandBehavior;
        Action resolveGlobalDbContext = () => scope.ResolveGlobalDbContext();

        // Assert
        resolveGlobalDbContext.ShouldThrow<InvalidOperationException>();
        commandBehavior.ShouldBeOfType<EfCoreCommandTransactionBehavior<TestCommand, int>>();
        otherCommandBehavior.ShouldBeOfType<EfCoreCommandTransactionBehavior<OtherTestCommand, int>>();
    }

    [Fact]
    public void Registering_closed_transaction_behavior_rejects_missing_services()
    {
        // Arrange
        IServiceCollection? services = null;
        Action registerBehavior = () => services!.AddEfCoreCommandTransaction<TestDbContext, TestCommand, int>();

        // Act
        var exception = registerBehavior.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void Registering_closed_transaction_behavior_rejects_queries()
    {
        // Arrange
        var services = new ServiceCollection();
        Action registerBehavior = () => services.AddEfCoreCommandTransaction<TestDbContext, TestQuery, int>();

        // Act
        var exception = registerBehavior.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("EF Core command transactions can only be registered for command requests.");
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
        Func<Task> handle = async () =>
            await behavior.Handle(command!, () => ValueTask.FromResult(1), TestContext.Current.CancellationToken);

        // Act
        var exception = await handle.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("request");
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
        Func<Task> handle = async () =>
            await behavior.Handle(new TestCommand(), next!, TestContext.Current.CancellationToken);

        // Act
        var exception = await handle.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("next");
    }
}
