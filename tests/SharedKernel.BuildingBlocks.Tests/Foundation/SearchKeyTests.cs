namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>Tests the accent- and case-insensitive search key of a text.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "SearchKey")]
public sealed class SearchKeyTests
{
    [Theory]
    [InlineData("Eslovênia", "eslovenia")]
    [InlineData("ESLOVÊNIA", "eslovenia")]
    [InlineData("Ação & Reação", "acao & reacao")]
    [InlineData("  São \t  Paulo\n", "sao paulo")]
    [InlineData("são-paulo", "sao-paulo")]
    [InlineData("Москва", "москва")]
    [InlineData("Ёлка", "елка")]
    [InlineData("東京 2027", "東京 2027")]
    [InlineData("한국어", "한국어")]
    [InlineData("Straße", "straße")]
    [InlineData("100% _off_", "100% _off_")]
    [InlineData("Ⅻ ＴＯＫＹＯ", "ⅻ ｔｏｋｙｏ")]
    [InlineData("a⃝b", "ab")]
    [InlineData("a\U0001F600b", "a\U0001F600b")]
    [InlineData("", "")]
    public void The_key_drops_marks_lowercases_and_collapses_white_space_and_keeps_every_letter_and_digit(string text, string expected)
    {
        // Arrange & Act
        var key = SearchKey.Of(text);

        // Assert
        key.ShouldBe(expected);
    }

    [Fact]
    public void A_missing_text_has_an_empty_key()
    {
        // Arrange & Act
        var key = SearchKey.Of(null);

        // Assert
        key.ShouldBeEmpty();
    }

    [Fact]
    public void A_lone_surrogate_becomes_the_replacement_character_instead_of_failing()
    {
        // Arrange & Act
        var key = SearchKey.Of("A\uD800B\uDC00");

        // Assert
        key.ShouldBe("a�b�");
    }

    [Fact]
    public void The_key_is_composed_so_it_compares_equal_to_the_same_composed_text()
    {
        // Arrange
        var decomposed = "été 가";

        // Act
        var key = SearchKey.Of(decomposed);

        // Assert
        key.ShouldBe("ete 가");
        key.IsNormalized(System.Text.NormalizationForm.FormC).ShouldBeTrue();
    }

    [Fact]
    public void A_key_is_its_own_key()
    {
        // Arrange
        var key = SearchKey.Of("  Viagem à PATAGÔNIA, 2027  ");

        // Act
        var again = SearchKey.Of(key);

        // Assert
        again.ShouldBe(key);
    }
}
