using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SharedKernel.Testing.Sqlite;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Tests the shared SQLite context configuration.</summary>
[Trait("Scope", "Integration")]
[Trait("Capability", "SqliteConfiguration")]
public sealed class SqliteDbContextConfigurationTests
{
    private const string ConnectionString = "Data Source=configured.db";

    [Fact]
    public void The_context_uses_the_connection_string_with_the_default_history_table()
    {
        // Arrange
        var configuration = new SqliteDbContextConfiguration<AccountDbContext>(ConnectionString);
        var options = new DbContextOptionsBuilder<AccountDbContext>();

        // Act
        configuration.ConfigureOptions(options);

        // Assert
        var relational = RelationalOptionsExtension.Extract(options.Options);
        relational.ConnectionString.ShouldBe(ConnectionString);
        relational.MigrationsHistoryTableName.ShouldBeNull();
        SqliteConfigurationProbe.Interceptors(options).ShouldBeEmpty();
        ((IDbContextConfiguration<AccountDbContext>)configuration).ContextType.ShouldBe(typeof(AccountDbContext));
    }

    [Fact]
    public void A_context_with_its_own_history_table_names_it()
    {
        // Arrange
        var configuration = new SqliteDbContextConfiguration<AccountDbContext>(ConnectionString) { MigrationsHistoryTable = "__SiteMigrationsHistory" };
        var options = new DbContextOptionsBuilder<AccountDbContext>();

        // Act
        configuration.ConfigureOptions(options);

        // Assert
        RelationalOptionsExtension.Extract(options.Options).MigrationsHistoryTableName.ShouldBe("__SiteMigrationsHistory");
    }

    [Fact]
    public async Task With_the_search_fold_every_connection_the_context_opens_has_the_function()
    {
        // Arrange
        using var database = new SqliteInMemoryDatabase("configured");
        var configuration = new SqliteDbContextConfiguration<PlaceDbContext>(database.ConnectionString) { UseSearchFold = true };
        var options = new DbContextOptionsBuilder<PlaceDbContext>();
        configuration.ConfigureOptions(options);
        await using var context = new PlaceDbContext(options.Options);

        // Act
        var folded = await SqliteConfigurationProbe.Fold(context, "ESLOVÊNIA");

        // Assert
        folded.ShouldBe("eslovenia");
    }

    [Fact]
    public async Task Without_the_search_fold_the_function_is_not_registered()
    {
        // Arrange
        using var database = new SqliteInMemoryDatabase("unconfigured");
        var configuration = new SqliteDbContextConfiguration<PlaceDbContext>(database.ConnectionString);
        var options = new DbContextOptionsBuilder<PlaceDbContext>();
        configuration.ConfigureOptions(options);
        await using var context = new PlaceDbContext(options.Options);

        // Act
        var thrown = await SqliteConfigurationProbe.FoldFailure(context, "ESLOVÊNIA");

        // Assert
        thrown.Message.ShouldContain(SearchFold.FunctionName, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_connection_string_is_rejected(string connectionString)
    {
        // Arrange
        Func<object?> create = () => new SqliteDbContextConfiguration<AccountDbContext>(connectionString);

        // Act & Assert
        create.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        // Arrange
        Func<object?> create = () => new SqliteDbContextConfiguration<AccountDbContext>(null!);
        Action configure = () => new SqliteDbContextConfiguration<AccountDbContext>(ConnectionString).ConfigureOptions(null!);

        // Act & Assert
        create.ShouldThrow<ArgumentNullException>();
        configure.ShouldThrow<ArgumentNullException>();
    }
}
