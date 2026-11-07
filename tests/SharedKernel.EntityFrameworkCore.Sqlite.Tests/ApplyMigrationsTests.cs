using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Tests applying the migrations of a context from the root service provider.</summary>
[Trait("Scope", "Integration")]
[Trait("Capability", "Migrations")]
public sealed class ApplyMigrationsTests
{
    [Fact]
    public async Task Applying_creates_the_schema_and_records_the_migration()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var provider = new ServiceCollection()
            .AddDbContext<LedgerDbContext>(options => options.UseSqlite(connection))
            .BuildServiceProvider();

        // Act
        await provider.ApplyMigrations<LedgerDbContext>(TestContext.Current.CancellationToken);

        // Assert
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        var applied = (await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken)).ToArray();
        applied.ShouldBe([CreateLedger.MigrationId]);
        var count = await context.Entries.CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(0);
    }

    [Fact]
    public async Task Applying_twice_changes_nothing_the_second_time()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var provider = new ServiceCollection()
            .AddDbContext<LedgerDbContext>(options => options.UseSqlite(connection))
            .BuildServiceProvider();
        await provider.ApplyMigrations<LedgerDbContext>(TestContext.Current.CancellationToken);

        // Act
        await provider.ApplyMigrations<LedgerDbContext>(TestContext.Current.CancellationToken);

        // Assert
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        var pending = (await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ToArray();
        pending.ShouldBe([]);
    }

    [Fact]
    public async Task A_null_service_provider_is_rejected()
    {
        // Act
        Func<Task> act = () => DatabaseMigrationExtensions.ApplyMigrations<LedgerDbContext>(null!);

        // Assert
        await act.ShouldThrow<ArgumentNullException>();
    }
}
