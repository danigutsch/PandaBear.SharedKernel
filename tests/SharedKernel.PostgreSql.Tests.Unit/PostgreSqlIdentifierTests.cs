namespace SharedKernel.PostgreSql.Tests;

/// <summary>Tests the PostgreSQL unquoted identifier rule that storage options and SQL names share.</summary>
public sealed class PostgreSqlIdentifierTests
{
    [Theory]
    [InlineData("messaging")]
    [InlineData("_private")]
    [InlineData("Outbox_Messages")]
    [InlineData("table_2")]
    [InlineData("a")]
    public void IsUnquoted_accepts_ascii_letters_digits_and_underscores_after_a_letter_or_underscore(string candidate)
    {
        // Act
        var isUnquoted = PostgreSqlIdentifier.IsUnquoted(candidate);

        // Assert
        isUnquoted.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("2tables")]
    [InlineData("invalid-schema")]
    [InlineData("outbox;drop")]
    [InlineData("transport messages")]
    [InlineData("quoted\"name")]
    [InlineData("price$")]
    [InlineData("café")]
    [InlineData("école")]
    [InlineData("name\0")]
    public void IsUnquoted_refuses_anything_else(string? candidate)
    {
        // Act
        var isUnquoted = PostgreSqlIdentifier.IsUnquoted(candidate);

        // Assert
        isUnquoted.ShouldBeFalse();
    }

    [Fact]
    public void IsUnquoted_accepts_63_bytes_and_refuses_64_because_the_server_truncates_longer_names()
    {
        // Arrange
        var longest = new string('a', 63);
        var tooLong = new string('a', 64);

        // Act
        var longestIsUnquoted = PostgreSqlIdentifier.IsUnquoted(longest);
        var tooLongIsUnquoted = PostgreSqlIdentifier.IsUnquoted(tooLong);

        // Assert
        longestIsUnquoted.ShouldBeTrue();
        tooLongIsUnquoted.ShouldBeFalse();
    }

    [Fact]
    public void IsUnquoted_refuses_a_name_with_a_multibyte_character_even_when_it_has_fewer_than_63_characters()
    {
        // Arrange
        var candidate = new string('a', 31) + "é";

        // Act
        var isUnquoted = PostgreSqlIdentifier.IsUnquoted(candidate);

        // Assert
        isUnquoted.ShouldBeFalse();
    }
}
