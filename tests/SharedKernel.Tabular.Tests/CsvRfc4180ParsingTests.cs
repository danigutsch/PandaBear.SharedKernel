using System.Text;
using PandaBear.Results;

namespace PandaBear.Tabular.Tests;

/// <summary>
/// Tests the RFC 4180 reading rules: quoted fields with doubled quotes, commas and line breaks; an optional byte order mark; CRLF or LF;
/// unquoted fields trimmed and quoted ones kept as they are; a stray quote inside an unquoted field kept literally; and blank lines.
/// Every case is checked through both <see cref="CsvDocument.Parse"/> and <see cref="CsvDocument.ParseUtf8"/>.
/// </summary>
public sealed class CsvRfc4180ParsingTests
{
    [Theory]
    [InlineData("Name,Note\n\"Doe, John\",x", "Doe, John", "x")]
    [InlineData("Name,Note\n\"say \"\"hi\"\"\",x", "say \"hi\"", "x")]
    [InlineData("Name,Note\n\"line one\nline two\",x", "line one\nline two", "x")]
    [InlineData("Name,Note\r\n\"line one\r\nline two\",x", "line one\r\nline two", "x")]
    [InlineData("Name,Note\n\"  padded  \",x", "  padded  ", "x")]
    [InlineData("Name,Note\n  \"spaced quotes\"  ,x", "spaced quotes", "x")]
    [InlineData("Name,Note\n\"\",x", "", "x")]
    [InlineData("Name,Note\n  plain  ,  x  ", "plain", "x")]
    [InlineData("Name,Note\nO\"Neil,x", "O\"Neil", "x")]
    [InlineData("Name,Note\nab\"c\"d,x", "ab\"c\"d", "x")]
    public void A_field_is_read_by_the_rfc_4180_rules(string content, string first, string second)
    {
        foreach (var document in CsvParsing.ParseBoth(content))
        {
            // Assert
            var row = document.Rows.ShouldHaveSingleItem();
            row[0].ShouldBe(first);
            row[1].ShouldBe(second);
        }
    }

    [Theory]
    [InlineData("﻿Name,Note\na,b")]
    [InlineData("\"Name\",\"Note\"\r\na,b")]
    [InlineData(" Name , Note \na,b")]
    public void Headers_follow_the_same_rules_and_a_byte_order_mark_is_removed(string content)
    {
        foreach (var document in CsvParsing.ParseBoth(content))
        {
            // Assert
            document.Headers.ShouldBe(["Name", "Note"]);
        }
    }

    [Theory]
    [InlineData("Name,Note\na,b\n")]
    [InlineData("Name,Note\r\na,b\r\n")]
    public void A_final_line_break_ends_the_last_record_and_adds_none(string content)
    {
        foreach (var document in CsvParsing.ParseBoth(content, new CsvParseOptions { SkipBlankLines = false }))
        {
            // Assert
            document.Rows.ShouldHaveSingleItem()[0].ShouldBe("a");
        }
    }

    [Fact]
    public void Blank_lines_are_skipped_by_default_and_line_numbers_still_count_them()
    {
        // Arrange
        const string content = "\n\nName,Note\n\na,b\n   \n\"x\ny\",z\n";

        foreach (var document in CsvParsing.ParseBoth(content))
        {
            // Assert
            document.Headers.ShouldBe(["Name", "Note"]);
            document.Rows.Count.ShouldBe(2);
            document.Rows[0].LineNumber.ShouldBe(5);
            document.Rows[1].LineNumber.ShouldBe(7);
            document.Rows[1][0].ShouldBe("x\ny");
        }
    }

    [Fact]
    public void A_quoted_empty_field_is_not_a_blank_line()
    {
        foreach (var document in CsvParsing.ParseBoth("Name\n\"\"\nb"))
        {
            // Assert
            document.Rows.Count.ShouldBe(2);
            document.Rows[0][0].ShouldBe(string.Empty);
        }
    }

    [Fact]
    public void Kept_blank_lines_are_rows_that_fail_the_column_count_on_their_own_line()
    {
        // Arrange
        var options = new CsvParseOptions { SkipBlankLines = false };

        // Act
        var text = CsvDocument.Parse("Name,Note\na,b\n\nc,d", options: options);
        var utf8 = CsvDocument.ParseUtf8("Name,Note\na,b\n\nc,d"u8, options: options);

        // Assert
        text.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid).Detail.ShouldContain("line 3", StringComparison.Ordinal);
        utf8.ErrorDetails!.Detail.ShouldBe(text.ErrorDetails!.Detail);
    }

    [Theory]
    [InlineData("Name,Note\n\"never closed,x", "line 2")]
    [InlineData("Name,Note\na,b\n\"open\nstill open", "line 3")]
    public void An_unterminated_quoted_field_is_refused_with_the_line_it_starts_on(string content, string line)
    {
        // Act
        var text = CsvDocument.Parse(content);
        var utf8 = CsvDocument.ParseUtf8(Encoding.UTF8.GetBytes(content));

        // Assert
        text.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid).Detail.ShouldContain(line, StringComparison.Ordinal);
        text.ErrorDetails!.Detail.ShouldContain("quoted", StringComparison.Ordinal);
        utf8.ErrorDetails!.Detail.ShouldBe(text.ErrorDetails!.Detail);
    }

    [Fact]
    public void Text_after_a_closing_quote_is_refused()
    {
        // Act
        var text = CsvDocument.Parse("Name,Note\n\"a\"b,x");
        var utf8 = CsvDocument.ParseUtf8("Name,Note\n\"a\"b,x"u8);

        // Assert
        text.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid).Detail.ShouldContain("line 2", StringComparison.Ordinal);
        utf8.ErrorDetails!.Detail.ShouldBe(text.ErrorDetails!.Detail);
    }

    [Fact]
    public void Multibyte_text_around_quotes_is_decoded_per_field()
    {
        foreach (var document in CsvParsing.ParseBoth("Nome,Cidade\n\"São, Paulo\",Zürich"))
        {
            // Assert
            document.Rows[0][0].ShouldBe("São, Paulo");
            document.Rows[0][1].ShouldBe("Zürich");
        }
    }
}
