using PandaBear.VaultManager.Tool.Formats.Csv;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Csv;

public sealed class CsvReaderTests
{
    [Fact]
    public void Parse_splits_plain_fields_and_records()
    {
        var records = CsvReader.Parse("a,b,c\nd,,f");

        records.Select(record => record.Fields).ShouldBe([["a", "b", "c"], ["d", "", "f"]]);
    }

    [Fact]
    public void Parse_reads_quoted_fields_with_commas_doubled_quotes_and_line_breaks()
    {
        var records = CsvReader.Parse("\"a,1\",\"say \"\"hi\"\"\",\"line1\r\nline2\nline3\"\r\nnext,\"\"");

        records[0].Fields.ShouldBe(["a,1", "say \"hi\"", "line1\r\nline2\nline3"]);
        records[1].Fields.ShouldBe(["next", ""]);
        records[1].LineNumber.ShouldBe(4);
    }

    [Fact]
    public void Parse_accepts_crlf_and_lf_line_endings_and_ignores_trailing_empty_line()
    {
        var crlf = CsvReader.Parse("a,b\r\nc,d\r\n");
        var lf = CsvReader.Parse("a,b\nc,d\n");

        crlf.Select(record => record.Fields).ShouldBe([["a", "b"], ["c", "d"]]);
        lf.Select(record => record.Fields).ShouldBe([["a", "b"], ["c", "d"]]);
    }

    [Fact]
    public void Parse_strips_byte_order_mark()
    {
        var record = CsvReader.Parse("\uFEFFfolder,name").ShouldHaveSingleItem();

        record.Fields[0].ShouldBe("folder");
    }

    [Fact]
    public void Parse_returns_no_records_for_empty_text()
    {
        CsvReader.Parse("").ShouldBeEmpty();
    }

    [Fact]
    public void Parse_unterminated_quote_throws_with_line_number_and_no_value()
    {
        var action = () => CsvReader.Parse("a,b\nc,\"secret-value\nmore");

        var exception = action.ShouldThrow<InvalidDataException>();

        exception.Message.ShouldContain("line 2", StringComparison.Ordinal);
        exception.Message.ShouldNotContain("secret-value", StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_text_after_closing_quote_throws_with_line_number()
    {
        var action = () => CsvReader.Parse("\"a\"b,c");

        var exception = action.ShouldThrow<InvalidDataException>();

        exception.Message.ShouldContain("line 1", StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_of_record_never_contains_values()
    {
        var record = CsvReader.Parse("secret-value,other").ShouldHaveSingleItem();

        record.ToString().ShouldNotContain("secret-value", StringComparison.Ordinal);
    }
}
