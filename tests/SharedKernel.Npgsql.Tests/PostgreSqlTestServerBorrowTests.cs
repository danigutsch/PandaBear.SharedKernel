namespace PandaBear.Npgsql.Tests;

public sealed class PostgreSqlTestServerBorrowTests
{
    [Fact]
    public void The_contract_names_the_variables_that_the_owner_host_and_the_ci_tool_agree_on()
    {
        // Act
        var connectionStringVariable = PostgreSqlTestServer.ConnectionStringVariable;
        var readyFileVariable = PostgreSqlTestServer.ReadyFileVariable;

        // Assert
        connectionStringVariable.ShouldBe("SHAREDKERNEL_TESTING_POSTGRESQL_CONNECTION_STRING", "the CI tool exports this name to the test phase");
        readyFileVariable.ShouldBe("SHAREDKERNEL_TESTING_POSTGRESQL_READY_FILE", "the CI tool passes this name to the owner host");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \r\n")]
    public void An_unset_or_blank_variable_means_no_borrowed_server(string? value)
    {
        // Arrange
        var environment = FakeEnvironment.WithServer(value);

        // Act
        var borrowed = PostgreSqlTestServer.Borrowed(environment);

        // Assert
        borrowed.ShouldBeNull("a blank variable means that the assembly starts a server of its own on demand");
    }

    [Theory]
    [InlineData("Host=db.example;Port=5432;Username=postgres;Password=owner-secret")]
    [InlineData("Host=127.0.0.1;Port=41234;Username=postgres;Password=owner-secret;Database=postgres")]
    [InlineData("Host=localhost")]
    public void A_valid_variable_is_borrowed_unchanged(string connectionString)
    {
        // Arrange
        var environment = FakeEnvironment.WithServer(connectionString);

        // Act
        var borrowed = PostgreSqlTestServer.Borrowed(environment);

        // Assert
        borrowed.ShouldBe(connectionString);
    }

    [Fact]
    public void Whitespace_around_a_valid_variable_is_ignored()
    {
        // Arrange
        var environment = FakeEnvironment.WithServer("  Host=db.example;Port=5432;Password=owner-secret\r\n");

        // Act
        var borrowed = PostgreSqlTestServer.Borrowed(environment);

        // Assert
        borrowed.ShouldBe("Host=db.example;Port=5432;Password=owner-secret", "a trailing newline copied from the owner's ready file must not become part of the password");
    }

    [Theory]
    [InlineData("not a connection string")]
    [InlineData("owner-secret")]
    [InlineData("Username=postgres;Password=owner-secret")]
    [InlineData("Host=;Password=owner-secret")]
    [InlineData("Host=localhost;Port=not-a-port;Password=owner-secret")]
    [InlineData("Host=localhost;Password=owner-secret;Unknown Setting=1")]
    public void A_malformed_variable_fails_loudly_and_never_echoes_its_value(string value)
    {
        // Arrange
        var environment = FakeEnvironment.WithServer(value);
        Func<object?> borrow = () => PostgreSqlTestServer.Borrowed(environment);

        // Act
        var failure = borrow.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(PostgreSqlTestServer.ConnectionStringVariable, "the message must tell the reader which variable to fix");
        failure.ToString().ShouldNotContain(value, "the value may carry the owner's password");
        failure.ToString().ShouldNotContain("owner-secret", "no part of the value may reach a log");
        failure.InnerException.ShouldBeNull("the parser's message may repeat a part of the value");
    }
}
