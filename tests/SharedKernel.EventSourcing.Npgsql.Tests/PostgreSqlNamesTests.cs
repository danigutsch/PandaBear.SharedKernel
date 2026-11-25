namespace SharedKernel.EventSourcing.Npgsql.Tests;

/// <summary>Tests the schema name that the event store splices into its SQL text.</summary>
public sealed class PostgreSqlNamesTests
{
    [Fact]
    public void A_63_byte_schema_is_accepted_and_quoted()
    {
        // Arrange
        var schema = new string('a', 63);

        // Act
        var quoted = PostgreSqlNames.Schema(new PostgreSqlEventSourcingOptions { Schema = schema });

        // Assert
        quoted.ShouldBe("\"" + schema + "\"");
    }

    [Fact]
    public void A_64_byte_schema_that_the_server_would_truncate_is_refused()
    {
        // Arrange
        var options = new PostgreSqlEventSourcingOptions { Schema = new string('a', 64) };

        Func<object?> resolve = () => PostgreSqlNames.SchemaName(options);

        // Act
        var exception = resolve.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("options");
        exception.Message.ShouldStartWith("PostgreSQL schema must be a valid unquoted identifier.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("event-sourcing")]
    [InlineData("events;drop")]
    [InlineData("événements")]
    public void A_schema_that_is_not_an_unquoted_identifier_is_refused(string schema)
    {
        // Arrange
        var options = new PostgreSqlEventSourcingOptions { Schema = schema };

        Func<object?> resolve = () => PostgreSqlNames.SchemaName(options);

        // Act
        var exception = resolve.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("options");
    }
}
