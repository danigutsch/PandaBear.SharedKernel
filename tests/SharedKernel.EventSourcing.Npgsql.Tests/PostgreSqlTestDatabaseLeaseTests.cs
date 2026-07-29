namespace SharedKernel.EventSourcing.Npgsql.Tests;

public sealed class PostgreSqlTestDatabaseLeaseTests
{
    [Theory]
    [InlineData("postgres")]
    [InlineData("test_not-a-guid")]
    public async Task Drop_database_rejects_non_fixture_owned_names_before_connecting(string databaseName)
    {
        // Arrange
        const string invalidConnectionString = "not-a-connection-string";

        // Act
        Func<Task> dropDatabase = () => PostgreSqlTestDatabaseLease.DropDatabase(
            invalidConnectionString,
            databaseName);
        var exception = await dropDatabase.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("databaseName");
    }
}
