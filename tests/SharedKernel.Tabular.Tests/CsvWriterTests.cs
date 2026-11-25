namespace SharedKernel.Tabular.Tests;

/// <summary>
/// Tests the RFC 4180 writer: CRLF records, quoting only where reading needs it, doubled quotes, and the formula-injection guard that
/// prefixes an apostrophe to a value a spreadsheet would run.
/// </summary>
public sealed class CsvWriterTests
{
    [Fact]
    public void A_document_is_written_as_crlf_records_with_quotes_only_where_needed()
    {
        // Arrange
        string[] headers = ["Name", "Note"];
        string[][] rows = [["Doe, John", "say \"hi\""], ["plain", "two\nlines"], ["  padded", ""]];

        // Act
        var csv = CsvWriter.Write(headers, rows);

        // Assert
        csv.ShouldBe("Name,Note\r\n\"Doe, John\",\"say \"\"hi\"\"\"\r\nplain,\"two\nlines\"\r\n\"  padded\",\r\n");
    }

    [Theory]
    [InlineData("=SUM(A1:A2)", "\"'=SUM(A1:A2)\"")]
    [InlineData("+1", "\"'+1\"")]
    [InlineData("-1", "\"'-1\"")]
    [InlineData("@cmd", "\"'@cmd\"")]
    [InlineData("  =1", "\"'  =1\"")]
    [InlineData("\t=1", "\"'\t=1\"")]
    [InlineData("＝1", "\"'＝1\"")]
    [InlineData("\r1", "\"'\r1\"")]
    [InlineData("a=1", "a=1")]
    [InlineData("", "")]
    public void The_formula_guard_prefixes_an_apostrophe_to_a_value_a_spreadsheet_would_run(string value, string expected)
    {
        // Act
        var field = CsvWriter.FormatField(value);

        // Assert
        field.ShouldBe(expected);
    }

    [Fact]
    public void The_formula_guard_can_be_turned_off()
    {
        // Arrange
        var options = new CsvWriteOptions { GuardFormulas = false };

        // Act
        var field = CsvWriter.FormatField("=SUM(A1:A2)", options);

        // Assert
        field.ShouldBe("=SUM(A1:A2)");
    }

    [Fact]
    public void A_single_empty_field_is_quoted_so_its_record_is_not_a_blank_line()
    {
        // Act
        var csv = CsvWriter.Write(["Name"], [[""], ["b"]]);

        // Assert
        csv.ShouldBe("Name\r\n\"\"\r\nb\r\n");
    }

    [Fact]
    public void A_line_ending_can_be_chosen()
    {
        // Act
        var csv = CsvWriter.Write(["A"], [["1"]], new CsvWriteOptions { NewLine = "\n" });

        // Assert
        csv.ShouldBe("A\n1\n");
    }

    [Fact]
    public void A_parsed_document_is_written_back()
    {
        // Arrange
        var document = CsvDocument.Parse("Name,Note\n\"Doe, John\",x").ShouldBeSuccess();

        // Act
        var csv = CsvWriter.Write(document.Headers, document.Rows);

        // Assert
        csv.ShouldBe("Name,Note\r\n\"Doe, John\",x\r\n");
    }

    [Fact]
    public void Duplicate_rows_are_reported_on_the_physical_line_they_start_on()
    {
        // Arrange
        var document = CsvDocument.Parse("Code,Note\nalpha,\"two\nlines\"\n\nalpha,x").ShouldBeSuccess();

        // Act
        var duplicates = DuplicateRowScanner.FindDuplicateLineNumbers(document, row => row[0]);

        // Assert
        duplicates.ShouldBe([5]);
    }

    [Fact]
    public void Rows_that_do_not_match_the_headers_or_no_headers_are_refused()
    {
        // Act
        Action noHeaders = () => CsvWriter.Write([], []);
        Action shortRow = () => CsvWriter.Write(["A", "B"], [["1"]]);
        Action blankNewLine = () => _ = new CsvWriteOptions { NewLine = "" };

        // Assert
        noHeaders.ShouldThrow<ArgumentException>();
        shortRow.ShouldThrow<ArgumentException>();
        blankNewLine.ShouldThrow<ArgumentException>();
    }
}
