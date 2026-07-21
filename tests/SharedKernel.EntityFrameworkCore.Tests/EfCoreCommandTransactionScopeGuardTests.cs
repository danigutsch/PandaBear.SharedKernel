using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

public sealed class EfCoreCommandTransactionScopeGuardTests
{
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
        response.ShouldBe(12);
    }

    [Fact]
    public async Task Rejects_missing_dbcontext()
    {
        // Arrange
        TestDbContext? dbContext = null;

        Func<Task> execute = async () =>
            await EfCoreCommandTransactionScope.Execute(dbContext!, () => ValueTask.FromResult(1), TestContext.Current.CancellationToken);

        // Act
        var exception = await execute.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("dbContext");
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

        Func<Task> execute = async () =>
            await EfCoreCommandTransactionScope.Execute(dbContext, next!, TestContext.Current.CancellationToken);

        // Act
        var exception = await execute.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("next");
    }
}
