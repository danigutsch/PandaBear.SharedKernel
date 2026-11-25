using System.Globalization;
using System.Text;

namespace SharedKernel.Tabular;

/// <summary>
/// Writes RFC 4180 CSV that <see cref="CsvDocument.Parse"/> reads back exactly: a value is quoted, with its quotes doubled, when it holds a
/// comma, quote or line break, starts or ends with white space (which reading would trim), starts with a byte order mark, or is guarded as a
/// formula; a record of one empty value is written as <c>""</c> so it is not read as a blank line. With
/// <see cref="CsvWriteOptions.GuardFormulas"/> off, <c>Parse(Write(headers, rows))</c> gives back the same headers and rows; with it on,
/// every guarded value comes back with its apostrophe and every other value unchanged.
/// </summary>
public static class CsvWriter
{
    /// <summary>Writes a document to a string.</summary>
    /// <param name="headers">The header values; at least one.</param>
    /// <param name="rows">The rows, each with one value per header; a <see cref="CsvRow"/> qualifies.</param>
    /// <param name="options">The options; <see cref="CsvWriteOptions.Default"/> when <see langword="null"/>.</param>
    /// <returns>The CSV text, every record ending with <see cref="CsvWriteOptions.NewLine"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/>, <paramref name="rows"/>, a row or a value is null.</exception>
    /// <exception cref="ArgumentException">There is no header, or a row's value count differs from the header count.</exception>
    public static string Write(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows, CsvWriteOptions? options = null)
    {
        var builder = new StringBuilder();
        using var writer = new StringWriter(builder, CultureInfo.InvariantCulture);
        Write(writer, headers, rows, options);
        return builder.ToString();
    }

    /// <summary>Writes a document to a text writer, which is not flushed or disposed.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="headers">The header values; at least one.</param>
    /// <param name="rows">The rows, each with one value per header; a <see cref="CsvRow"/> qualifies.</param>
    /// <param name="options">The options; <see cref="CsvWriteOptions.Default"/> when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/>, <paramref name="headers"/>, <paramref name="rows"/>, a row or a value is null.</exception>
    /// <exception cref="ArgumentException">There is no header, or a row's value count differs from the header count.</exception>
    public static void Write(TextWriter writer, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows, CsvWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);
        if (headers.Count == 0)
        {
            throw new ArgumentException("Write at least one header.", nameof(headers));
        }

        options ??= CsvWriteOptions.Default;
        WriteRecord(writer, headers, options);
        foreach (var row in rows)
        {
            ArgumentNullException.ThrowIfNull(row, nameof(rows));
            if (row.Count != headers.Count)
            {
                throw new ArgumentException($"Every row must have {headers.Count} values, one per header.", nameof(rows));
            }

            WriteRecord(writer, row, options);
        }
    }

    /// <summary>Formats one value as a CSV field, quoted and guarded as <see cref="Write(IReadOnlyList{string}, IEnumerable{IReadOnlyList{string}}, CsvWriteOptions?)"/> does.</summary>
    /// <param name="value">The value.</param>
    /// <param name="options">The options; <see cref="CsvWriteOptions.Default"/> when <see langword="null"/>.</param>
    /// <returns>The field text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static string FormatField(string value, CsvWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        var guarded = (options ?? CsvWriteOptions.Default).GuardFormulas && IsFormula(value);
        var text = guarded ? "'" + value : value;
        return guarded || NeedsQuotes(text) ? Quote(text) : text;
    }

    private static void WriteRecord(TextWriter writer, IReadOnlyList<string> values, CsvWriteOptions options)
    {
        if (values is [{ Length: 0 }])
        {
            writer.Write("\"\"");
        }
        else
        {
            for (var index = 0; index < values.Count; index++)
            {
                if (index > 0)
                {
                    writer.Write(',');
                }

                writer.Write(FormatField(values[index] ?? throw new ArgumentNullException(nameof(values)), options));
            }
        }

        writer.Write(options.NewLine);
    }

    private static bool IsFormula(string value)
    {
        if (value.Length > 0 && char.IsControl(value[0]))
        {
            return true;
        }

        var start = 0;
        while (start < value.Length && (char.IsWhiteSpace(value[start]) || char.IsControl(value[start])))
        {
            start++;
        }

        return start < value.Length && value[start] is '=' or '+' or '-' or '@' or '＝' or '＋' or '－' or '＠';
    }

    private static bool NeedsQuotes(string value) =>
        value.Length > 0
        && (value.AsSpan().IndexOfAny(",\"\r\n") >= 0
            || char.IsWhiteSpace(value[0])
            || char.IsWhiteSpace(value[^1])
            || value[0] == '﻿');

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
