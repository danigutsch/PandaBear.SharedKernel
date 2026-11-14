using System.Text;

namespace PandaBear.Tabular.Tests;

/// <summary>
/// Property-style tests of the round-trip guarantee: for random documents built from every character the format treats specially,
/// <c>Parse(Write(x))</c> and <c>ParseUtf8(Write(x))</c> give back <c>x</c> exactly, whatever the blank-line option; with the formula guard
/// on, only the values it guards come back with their apostrophe.
/// </summary>
public sealed class CsvRoundTripTests
{
    private const int Iterations = 500;

    [Fact]
    public void Writing_then_parsing_gives_back_every_document_when_the_guard_is_off()
    {
        // Arrange
        var random = new Random(20261111);
        var options = new CsvWriteOptions { GuardFormulas = false };

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            var (headers, rows) = CsvSamples.RandomDocument(random);

            // Act
            var csv = CsvWriter.Write(headers, rows, options);

            // Assert
            foreach (var skipBlankLines in (bool[])[true, false])
            {
                var parseOptions = new CsvParseOptions { SkipBlankLines = skipBlankLines };
                CsvSamples.ShouldMatch(CsvDocument.Parse(csv, options: parseOptions).ShouldBeSuccess(csv), headers, rows, csv);
                CsvSamples.ShouldMatch(CsvDocument.ParseUtf8(Encoding.UTF8.GetBytes(csv), options: parseOptions).ShouldBeSuccess(csv), headers, rows, csv);
            }
        }
    }

    [Fact]
    public void With_the_guard_on_only_guarded_values_come_back_with_their_apostrophe()
    {
        // Arrange
        var random = new Random(4180);

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            var (headers, rows) = CsvSamples.RandomDocument(random);
            var expectedRows = rows.Select(row => row.Select(CsvSamples.Guarded).ToArray()).ToArray();
            var expectedHeaders = headers.Select(CsvSamples.Guarded).ToArray();

            // Act
            var csv = CsvWriter.Write(headers, rows);

            // Assert
            CsvSamples.ShouldMatch(CsvDocument.Parse(csv).ShouldBeSuccess(csv), expectedHeaders, expectedRows, csv);
        }
    }

    [Fact]
    public void Every_value_without_a_formula_start_round_trips_with_the_guard_on()
    {
        // Arrange
        string[] headers = ["Name", "Note", "Amount"];
        string[][] rows = [["Doe, John", "a=b", "1-2"], [" x ", "\"quoted\"", "line\r\nbreak"], ["", "''", "﻿bom"]];

        // Act
        var csv = CsvWriter.Write(headers, rows);

        // Assert
        CsvSamples.ShouldMatch(CsvDocument.Parse(csv).ShouldBeSuccess(csv), headers, rows, csv);
    }
}
