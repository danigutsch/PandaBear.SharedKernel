using SharedKernel.BuildingBlocks;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Tests the contains-pattern built from user input.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "SearchFold")]
public sealed class SearchFoldTests
{
    [Theory]
    [InlineData("Ê", "%e%")]
    [InlineData(" Eslov  Ênia ", "%eslov enia%")]
    [InlineData("a_b%c", "%a\\_b\\%c%")]
    [InlineData("back\\slash", "%back\\\\slash%")]
    [InlineData("Москва", "%москва%")]
    public void The_contains_pattern_wraps_the_escaped_key_of_the_input(string input, string expected)
    {
        // Arrange & Act
        var pattern = SearchFold.ContainsPattern(input);

        // Assert
        pattern.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("%")]
    [InlineData("_%_ -")]
    [InlineData("\U0001F600")]
    public void Input_without_a_letter_or_digit_has_no_pattern_so_it_matches_nothing(string? input)
    {
        // Arrange & Act
        var pattern = SearchFold.ContainsPattern(input);

        // Assert
        pattern.ShouldBeNull();
    }

    [Fact]
    public void The_mapped_function_computes_the_building_block_key()
    {
        // Arrange
        const string text = "  ESLOVÊNIA  ";

        // Act
        var key = SearchFold.Of(text);

        // Assert
        key.ShouldBe(SearchKey.Of(text));
        SearchFold.EscapeCharacter.ShouldBe("\\");
    }
}
