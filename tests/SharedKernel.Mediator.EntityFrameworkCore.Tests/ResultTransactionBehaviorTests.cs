using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PandaBear.Results;

namespace PandaBear.Mediator.EntityFrameworkCore.Tests;

/// <summary>Tests the result transaction behavior on a non-relational provider; PostgreSQL behavior is covered in the EntityFrameworkCore tests.</summary>
public sealed class ResultTransactionBehaviorTests
{
    [Fact]
    public async Task Non_relational_providers_run_the_next_handler_without_a_transaction()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var dbContext = new TestDbContext(options);
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var behavior = new ResultTransactionBehavior<ResultTransactionCommand, Result>(
            dbContext,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger.Instance);

        // Act
        var result = await behavior.Handle(
            new ResultTransactionCommand(),
            () => ValueTask.FromResult(Result.NotFound("missing")),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }
}
