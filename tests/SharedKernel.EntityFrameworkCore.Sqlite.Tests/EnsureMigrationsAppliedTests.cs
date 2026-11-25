using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>
/// Tests the start-up check of a host that only reads a database another host migrates: it fails while a migration is pending and never
/// writes, so it works on a read-only connection.
/// </summary>
[Trait("Scope", "Integration")]
[Trait("Capability", "Migrations")]
public sealed class EnsureMigrationsAppliedTests
{
    [Fact]
    public async Task A_database_without_the_migrations_fails_with_the_pending_count_and_first_migration()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var provider = new ServiceCollection()
            .AddDbContext<LedgerDbContext>(options => options.UseSqlite(connection))
            .BuildServiceProvider();

        // Act
        Func<Task> act = () => provider.EnsureMigrationsApplied<LedgerDbContext>(TestContext.Current.CancellationToken);

        // Assert
        var exception = await act.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain(nameof(LedgerDbContext), StringComparison.Ordinal);
        exception.Message.ShouldContain("1 migration(s)", StringComparison.Ordinal);
        exception.Message.ShouldContain(CreateLedger.MigrationId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_migrated_database_opened_read_only_passes_without_a_write()
    {
        // Arrange
        using var file = new TemporaryLedgerFile();
        await using (var writer = file.Services(SqliteOpenMode.ReadWriteCreate))
        {
            await writer.ApplyMigrations<LedgerDbContext>(TestContext.Current.CancellationToken);
        }

        var before = File.GetLastWriteTimeUtc(file.Path);
        await using var reader = file.Services(SqliteOpenMode.ReadOnly);

        // Act
        await reader.EnsureMigrationsApplied<LedgerDbContext>(TestContext.Current.CancellationToken);

        // Assert
        File.GetLastWriteTimeUtc(file.Path).ShouldBe(before);
    }

    [Fact]
    public async Task A_missing_database_file_counts_as_a_database_without_migrations_and_is_not_created()
    {
        // Arrange
        using var file = new TemporaryLedgerFile();
        await using var reader = file.Services(SqliteOpenMode.ReadOnly);

        // Act
        Func<Task> act = () => reader.EnsureMigrationsApplied<LedgerDbContext>(TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrow<InvalidOperationException>();
        File.Exists(file.Path).ShouldBeFalse();
    }

    [Fact]
    public async Task A_null_service_provider_is_rejected()
    {
        // Act
        Func<Task> act = () => DatabaseMigrationExtensions.EnsureMigrationsApplied<LedgerDbContext>(null!);

        // Assert
        await act.ShouldThrow<ArgumentNullException>();
    }
}
