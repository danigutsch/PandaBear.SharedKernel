using System.Text;

namespace PandaBear.Tabular.Tests;

/// <summary>Tests that parsing UTF-8 bytes yields the same document or error as parsing the decoded text.</summary>
public sealed class CsvDocumentUtf8ParsingTests
{
    [Theory]
    [InlineData("First,Second\na,b")]
    [InlineData("First,Second\r\na,b\r\nc,d")]
    [InlineData(" First , Second \n a , b \n,")]
    [InlineData("First,Second,Third\n\"x,y\",z\u00e9")]
    [InlineData("First,Second\n a , b")]
    [InlineData("﻿First,Second\na,b")]
    [InlineData("OnlyHeader")]
    [InlineData("First,Second\na,b\r")]
    public void ParseUtf8_yields_the_same_fields_as_parsing_the_decoded_text(string text)
    {
        // Arrange
        var expected = CsvDocument.Parse(text).Value;

        // Act
        var actual = CsvDocument.ParseUtf8(Encoding.UTF8.GetBytes(text)).Value;

        // Assert
        actual.Headers.ShouldBe(expected.Headers);
        actual.Rows.Count.ShouldBe(expected.Rows.Count);
        for (var row = 0; row < expected.Rows.Count; row++)
        {
            actual.Rows[row].Count.ShouldBe(expected.Rows[row].Count);
            for (var column = 0; column < expected.Rows[row].Count; column++)
            {
                actual.Rows[row][column].ShouldBe(expected.Rows[row][column]);
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \na,b")]
    [InlineData(" \na,b")]
    [InlineData("First,Second\na,b,c")]
    [InlineData("First,Second\na,b\nc")]
    [InlineData("First,Second\na,b\n")]
    [InlineData("First,Second\r\na,b\r\n")]
    [InlineData("First,Second\r\n\r\na,b")]
    public void ParseUtf8_fails_with_the_same_error_as_parsing_the_decoded_text(string text)
    {
        // Arrange
        var expected = CsvDocument.Parse(text);

        // Act
        var actual = CsvDocument.ParseUtf8(Encoding.UTF8.GetBytes(text));

        // Assert
        expected.IsFailure.ShouldBeTrue();
        actual.IsFailure.ShouldBeTrue();
        actual.ErrorDetails!.Detail.ShouldBe(expected.ErrorDetails!.Detail);
    }

    [Fact]
    public void ParseUtf8_reports_a_missing_required_header()
    {
        // Arrange
        var bytes = "First,Second\na,b"u8.ToArray();
        string[] required = ["first", "Third"];

        // Act
        var actual = CsvDocument.ParseUtf8(bytes, required);

        // Assert
        actual.IsFailure.ShouldBeTrue();
        actual.ErrorDetails!.Detail.ShouldBe(CsvDocument.Parse("First,Second\na,b", required).ErrorDetails!.Detail);
    }

    [Fact]
    public void ParseUtf8_replaces_invalid_utf8_like_the_text_decoder()
    {
        // Arrange
        byte[] bytes = [.. "First,Second\na,"u8, 0xFF, 0xFE, (byte)'b'];
        var expected = CsvDocument.Parse(Encoding.UTF8.GetString(bytes)).Value;

        // Act
        var actual = CsvDocument.ParseUtf8(bytes).Value;

        // Assert
        actual.Rows[0][1].ShouldBe(expected.Rows[0][1]);
    }
}
