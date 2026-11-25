using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>
/// Tests that the start-up migration helpers skip a context on a non-relational provider, such as the in-memory provider of a test host,
/// instead of failing on relational-only calls.
/// </summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class NonRelationalMigrationTests
{
    [Fact]
    public async Task Applying_migrations_on_a_non_relational_provider_is_skipped_and_reported()
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddDbContext<UniqueRuleDbContext>(options => options.UseInMemoryDatabase(nameof(NonRelationalMigrationTests)))
            .BuildServiceProvider();

        // Act
        var migrated = await provider.ApplyMigrations<UniqueRuleDbContext>(TestContext.Current.CancellationToken);

        // Assert
        migrated.ShouldBeFalse();
    }

    [Fact]
    public async Task Checking_migrations_on_a_non_relational_provider_passes()
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddDbContext<UniqueRuleDbContext>(options => options.UseInMemoryDatabase(nameof(NonRelationalMigrationTests)))
            .BuildServiceProvider();

        // Act
        var thrown = await Record.ExceptionAsync(() => provider.EnsureMigrationsApplied<UniqueRuleDbContext>(TestContext.Current.CancellationToken));

        // Assert
        thrown.ShouldBeNull();
    }
}
