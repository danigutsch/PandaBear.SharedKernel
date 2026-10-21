namespace PandaBear.Tabular.Tests;

public sealed class CsvParsingSemanticsTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Parse_retains_trailing_empty_rows(string newline)
    {
        var result = CsvDocument.Parse("First,Second" + newline + "a,b" + newline);

        result.IsFailure.ShouldBeTrue();
        result.ErrorDetails!.Detail.ShouldContain("line 3", StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_preserves_empty_fields_and_splits_quoted_commas_literally()
    {
        var row = CsvRow.Parse("one,,\"two,three\"");

        row.Count.ShouldBe(4);
        row[1].ShouldBe(string.Empty);
        row[2].ShouldBe("\"two");
        row[3].ShouldBe("three\"");
    }

    [Fact]
    public void Parse_uses_first_case_insensitive_header_match()
    {
        var document = CsvDocument.Parse(" Code ,CODE\nfirst,second").Value;

        document.Rows[0][document.Headers, " code "].ShouldBe("first");
    }

    [Fact]
    public void Parse_guards_null_input()
    {
        Action parseDocument = () => CsvDocument.Parse(null!);
        Action parseRow = () => CsvRow.Parse(null!);

        parseDocument.ShouldThrow<ArgumentNullException>();
        parseRow.ShouldThrow<ArgumentNullException>();
    }
}
