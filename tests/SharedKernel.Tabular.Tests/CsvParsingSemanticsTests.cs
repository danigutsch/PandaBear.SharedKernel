namespace PandaBear.Tabular.Tests;

/// <summary>Tests for the literal CSV parsing rules: trailing empty rows, naive comma splitting, header matching and null guards.</summary>
public sealed class CsvParsingSemanticsTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Parse_retains_trailing_empty_rows(string newline)
    {
        // Act
        var result = CsvDocument.Parse("First,Second" + newline + "a,b" + newline);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorDetails!.Detail.ShouldContain("line 3", StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_preserves_empty_fields_and_splits_quoted_commas_literally()
    {
        // Act
        var row = CsvRow.Parse("one,,\"two,three\"");

        // Assert
        row.Count.ShouldBe(4);
        row[1].ShouldBe(string.Empty);
        row[2].ShouldBe("\"two");
        row[3].ShouldBe("three\"");
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
        Action parseRow = () => CsvRow.Parse(null!);

        // Act, Assert
        parseDocument.ShouldThrow<ArgumentNullException>();
        parseRow.ShouldThrow<ArgumentNullException>();
    }
}
