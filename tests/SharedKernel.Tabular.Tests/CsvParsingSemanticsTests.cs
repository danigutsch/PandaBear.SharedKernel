namespace PandaBear.Tabular.Tests;

/// <summary>Tests for the parsing rules kept from the simple dialect: trailing line breaks, empty fields, header matching and null guards.</summary>
public sealed class CsvParsingSemanticsTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void A_trailing_line_break_adds_no_row(string newline)
    {
        // Act
        var document = CsvDocument.Parse("First,Second" + newline + "a,b" + newline).ShouldBeSuccess();

        // Assert
        document.Rows.ShouldHaveSingleItem().LineNumber.ShouldBe(2);
    }

    [Fact]
    public void Parse_preserves_empty_fields_and_keeps_a_quoted_comma_in_its_field()
    {
        // Act
        var row = CsvDocument.Parse("A,B,C\none,,\"two,three\"").ShouldBeSuccess().Rows.ShouldHaveSingleItem();

        // Assert
        row.Count.ShouldBe(3);
        row[1].ShouldBe(string.Empty);
        row[2].ShouldBe("two,three");
    }

    [Fact]
    public void Parse_uses_first_case_insensitive_header_match()
    {
        // Act
        var document = CsvDocument.Parse(" Code ,CODE\nfirst,second").Value;

        // Assert
        document.Rows[0][document.Headers, " code "].ShouldBe("first");
    }

    [Fact]
    public void Parse_guards_null_input()
    {
        // Arrange
        Action parseDocument = () => CsvDocument.Parse(null!);
        Action createRow = () => CsvRow.Create(null!);

        // Act, Assert
        parseDocument.ShouldThrow<ArgumentNullException>();
        createRow.ShouldThrow<ArgumentNullException>();
    }
}
