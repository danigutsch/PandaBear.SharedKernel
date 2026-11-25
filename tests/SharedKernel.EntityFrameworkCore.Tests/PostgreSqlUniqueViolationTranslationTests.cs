using Microsoft.EntityFrameworkCore;
using Npgsql;
using SharedKernel.BuildingBlocks;
using SharedKernel.EntityFrameworkCore.Npgsql;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>
/// Tests on a real PostgreSQL server that <see cref="DbContextUniqueViolationExtensions.SaveChangesTranslatingUniqueViolations"/> with the
/// PostgreSQL parser reports a tagged unique index by its rule and lets every other failure through unchanged.
/// </summary>
/// <param name="fixture">Shared PostgreSQL fixture that supplies the isolated database.</param>
[Trait(TestTraitNames.CategoryName, IntegrationTestTraitValues.DatabaseIntegrationCategory)]
public sealed class PostgreSqlUniqueViolationTranslationTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly NpgsqlUniqueViolationParser Parser = new();

    private PostgreSqlTestDatabaseLease? database;
    private NpgsqlDataSource? dataSource;

    private NpgsqlDataSource DataSource => dataSource ?? throw new InvalidOperationException("The database is not initialized.");

    /// <summary>Leases an isolated database and creates the schema.</summary>
    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        database = await fixture.CreateIsolatedDatabase(ct);
        dataSource = database.CreateDataSource(nameof(PostgreSqlUniqueViolationTranslationTests));
        await using var context = UniqueRuleDbContext.Create(DataSource);
        await context.Database.EnsureCreatedAsync(ct);
    }

    /// <summary>Releases the data source and the database.</summary>
    public async ValueTask DisposeAsync()
    {
        if (dataSource is not null)
        {
            await dataSource.DisposeAsync();
        }

        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }

    [Fact]
    public async Task A_duplicate_in_a_tagged_unique_index_is_reported_by_its_rule()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using (var first = UniqueRuleDbContext.Create(DataSource))
        {
            first.Members.Add(new UniqueRuleMember { Email = "same@example.com" });
            await first.SaveChangesAsync(ct);
        }

        await using var second = UniqueRuleDbContext.Create(DataSource);
        second.Members.Add(new UniqueRuleMember { Email = "same@example.com" });

        // Act
        var save = async () => await second.SaveChangesTranslatingUniqueViolations(Parser, ct);
        var thrown = await save.ShouldThrow<UniqueConstraintViolationException>();

        // Assert
        thrown.Rule.ShouldBe(UniqueRuleDbContext.EmailRule);
        thrown.InnerException.ShouldBeOfType<DbUpdateException>().InnerException.ShouldBeOfType<PostgresException>()
            .SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task A_duplicate_in_an_untagged_unique_index_propagates_unchanged()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using (var first = UniqueRuleDbContext.Create(DataSource))
        {
            first.Members.Add(new UniqueRuleMember { Email = "one@example.com", Nickname = "same" });
            await first.SaveChangesAsync(ct);
        }

        await using var second = UniqueRuleDbContext.Create(DataSource);
        second.Members.Add(new UniqueRuleMember { Email = "two@example.com", Nickname = "same" });

        // Act
        var save = async () => await second.SaveChangesTranslatingUniqueViolations(Parser, ct);
        var thrown = await save.ShouldThrow<DbUpdateException>();

        // Assert
        thrown.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }
}
