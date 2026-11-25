namespace SharedKernel.Tabular.Tests;

/// <summary>Random CSV documents and the comparisons of the round-trip tests.</summary>
internal static class CsvSamples
{
    // Every character the reader or writer treats specially, plus ordinary and multibyte text.
    private static readonly string[] Pieces =
    [
        ",", "\"", "\r", "\n", "\r\n", " ", "\t", " ", "﻿", "=", "+", "-", "@", "＝", "'", "a", "Z", "9", "ã", "ü", "😀",
    ];

    /// <summary>Creates a document of one to four columns and zero to five rows of random values, including empty ones.</summary>
    /// <param name="random">The seeded random source.</param>
    /// <returns>The headers and rows.</returns>
    internal static (string[] Headers, string[][] Rows) RandomDocument(Random random)
    {
        var columns = random.Next(1, 5);
        var headers = Enumerable.Range(0, columns).Select(_ => Value(random)).ToArray();
        var rows = Enumerable.Range(0, random.Next(0, 6))
            .Select(_ => Enumerable.Range(0, columns).Select(_ => Value(random)).ToArray())
            .ToArray();
        return (headers, rows);
    }

    /// <summary>The value the reader returns for a value written with the formula guard on.</summary>
    /// <param name="value">The written value.</param>
    /// <returns>The value with an apostrophe in front when the guard applies.</returns>
    internal static string Guarded(string value)
    {
        var start = value.AsSpan().TrimStart();
        var isFormula = !start.IsEmpty && start[0] is '=' or '+' or '-' or '@' or '＝' or '＋' or '－' or '＠';
        return isFormula || (value.Length > 0 && char.IsControl(value[0])) ? "'" + value : value;
    }

    /// <summary>Asserts that a parsed document holds exactly the given headers and rows.</summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="headers">The expected headers.</param>
    /// <param name="rows">The expected rows.</param>
    /// <param name="csv">The CSV text, reported on failure.</param>
    internal static void ShouldMatch(CsvDocument document, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows, string csv)
    {
        document.Headers.ShouldBe(headers, csv);
        document.Rows.Count.ShouldBe(rows.Count, csv);
        for (var row = 0; row < rows.Count; row++)
        {
            document.Rows[row].ToArray().ShouldBe(rows[row], csv);
        }
    }

    private static string Value(Random random)
    {
        var length = random.Next(0, 6);
        return string.Concat(Enumerable.Range(0, length).Select(_ => Pieces[random.Next(Pieces.Length)]));
    }
}
