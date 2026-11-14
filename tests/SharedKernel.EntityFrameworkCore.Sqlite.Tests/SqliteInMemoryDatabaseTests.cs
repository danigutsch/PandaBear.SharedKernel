using PandaBear.Testing.Sqlite;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Tests SqliteInMemoryDatabase: a private in-memory database per test that lives while the object lives and is gone after disposal.</summary>
public sealed class SqliteInMemoryDatabaseTests
{
    [Fact]
    public void The_database_survives_its_connections_while_the_object_lives()
    {
        // Arrange
        using var database = new SqliteInMemoryDatabase();
        SqliteTables.CreateTours(database.ConnectionString);

        // Act
        var exists = SqliteTables.ToursExist(database.ConnectionString);

        // Assert
        exists.ShouldBeTrue("the open connection the object holds keeps the in-memory database alive");
    }

    [Fact]
    public void Two_databases_never_share_tables()
    {
        // Arrange
        using var first = new SqliteInMemoryDatabase();
        using var second = new SqliteInMemoryDatabase();
        SqliteTables.CreateTours(first.ConnectionString);

        // Act
        var seenBySecond = SqliteTables.ToursExist(second.ConnectionString);

        // Assert
        seenBySecond.ShouldBeFalse();
        first.ConnectionString.ShouldNotBe(second.ConnectionString);
    }

    [Fact]
    public void Disposal_releases_the_pooled_connections_so_the_database_is_gone()
    {
        // Arrange
        var database = new SqliteInMemoryDatabase();
        var connectionString = database.ConnectionString;
        SqliteTables.CreateTours(connectionString);

        // Act
        database.Dispose();
        var stillThere = SqliteTables.ToursExist(connectionString);

        // Assert
        stillThere.ShouldBeFalse("a pooled connection must not keep a disposed in-memory database alive");
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("a b")]
    [InlineData("x?mode=rw")]
    public void A_name_that_is_not_letters_digits_or_dashes_is_refused(string name)
    {
        // Arrange
        // Act
        Action create = () => new SqliteInMemoryDatabase(name).Dispose();

        // Assert
        create.ShouldThrow<ArgumentException>().ParamName.ShouldBe("name");
    }
}
