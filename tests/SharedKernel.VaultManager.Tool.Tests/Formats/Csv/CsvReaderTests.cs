using PandaBear.VaultManager.Tool.Formats.Csv;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Csv;

/// <summary>Tests the CSV parser used by the CSV format readers.</summary>
public sealed class CsvReaderTests
{
    [Fact]
    public void Parse_splits_plain_fields_and_records()
    {
        // Act
        var records = CsvReader.Parse("a,b,c\nd,,f");

        // Assert
        records.Select(record => record.Fields).ShouldBe([["a", "b", "c"], ["d", "", "f"]]);
    }

    [Fact]
    public void Parse_reads_quoted_fields_with_commas_doubled_quotes_and_line_breaks()
    {
        // Act
        var records = CsvReader.Parse("\"a,1\",\"say \"\"hi\"\"\",\"line1\r\nline2\nline3\"\r\nnext,\"\"");

        // Assert
        records[0].Fields.ShouldBe(["a,1", "say \"hi\"", "line1\r\nline2\nline3"]);
        records[1].Fields.ShouldBe(["next", ""]);
        records[1].LineNumber.ShouldBe(4);
    }

    [Fact]
    public void Parse_accepts_crlf_and_lf_line_endings_and_ignores_trailing_empty_line()
    {
        // Act
        var crlf = CsvReader.Parse("a,b\r\nc,d\r\n");
        var lf = CsvReader.Parse("a,b\nc,d\n");

        // Assert
        crlf.Select(record => record.Fields).ShouldBe([["a", "b"], ["c", "d"]]);
        lf.Select(record => record.Fields).ShouldBe([["a", "b"], ["c", "d"]]);
    }

    [Fact]
    public void Parse_strips_byte_order_mark()
    {
        // Act
        var records = CsvReader.Parse("\uFEFFfolder,name");

        // Assert
        var record = records.ShouldHaveSingleItem();
        record.Fields[0].ShouldBe("folder");
    }

    [Fact]
    public void Parse_returns_no_records_for_empty_text()
    {
        // Act
        var records = CsvReader.Parse("");

        // Assert
        records.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_unterminated_quote_throws_with_line_number_and_no_value()
    {
        // Arrange
        var action = () => CsvReader.Parse("a,b\nc,\"secret-value\nmore");

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain("line 2", StringComparison.Ordinal);
        exception.Message.ShouldNotContain("secret-value", StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_text_after_closing_quote_throws_with_line_number()
    {
        // Arrange
        var action = () => CsvReader.Parse("\"a\"b,c");

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain("line 1", StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_of_record_never_contains_values()
    {
        // Arrange
        var record = CsvReader.Parse("secret-value,other").ShouldHaveSingleItem();

        // Act
        string text = record.ToString();

        // Assert
        text.ShouldNotContain("secret-value", StringComparison.Ordinal);
    }
}
