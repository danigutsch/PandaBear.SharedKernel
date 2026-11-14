using System.Collections;

namespace PandaBear.Tabular;

/// <summary>A record of a CSV document: its field values and, for a parsed record, the line it starts on.</summary>
public sealed class CsvRow : IReadOnlyList<string>
{
    private readonly string[] _values;

    private CsvRow(string[] values, int lineNumber)
    {
        _values = values;
        LineNumber = lineNumber;
    }

    /// <summary>Gets the number of fields, including empty fields.</summary>
    public int Count => _values.Length;

    /// <summary>
    /// Gets the one-based physical line the record starts on in the parsed content, counting skipped blank lines and the line breaks inside
    /// quoted fields; zero for a row made with <see cref="Create"/>.
    /// </summary>
    public int LineNumber
    {
        get;
    }

    /// <summary>Gets the field at the zero-based column index.</summary>
    /// <param name="index">The zero-based column index.</param>
    /// <returns>The field value.</returns>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the row.</exception>
    public string this[int index] => _values[index];

    /// <summary>Gets the field under the first matching header.</summary>
    /// <param name="headers">The headers aligned with this row.</param>
    /// <param name="headerName">The header name, trimmed and compared ignoring ordinal case.</param>
    /// <returns>The field under the matching header.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> or <paramref name="headerName"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The header is not present.</exception>
    /// <exception cref="IndexOutOfRangeException">The matching header index is outside this row.</exception>
    public string this[IReadOnlyList<string> headers, string headerName]
    {
        get
        {
            if (!TryGetByHeader(headers, headerName, out var value))
            {
                throw new KeyNotFoundException($"Header '{headerName}' was not found.");
            }

            return value!;
        }
    }

    /// <summary>Creates a row from field values, kept exactly as given.</summary>
    /// <param name="values">The field values.</param>
    /// <returns>The row, with <see cref="LineNumber"/> zero.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> or one of its values is null.</exception>
    public static CsvRow Create(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        string[] copy = [.. values];
        foreach (var value in copy)
        {
            ArgumentNullException.ThrowIfNull(value, nameof(values));
        }

        return new CsvRow(copy, 0);
    }

    /// <summary>Tries to read the field under the first matching header.</summary>
    /// <param name="headers">The headers aligned with this row.</param>
    /// <param name="headerName">The header name, trimmed and compared ignoring ordinal case.</param>
    /// <param name="value">The matching value, or null when the header is absent.</param>
    /// <returns>Whether a matching header exists.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> or <paramref name="headerName"/> is null.</exception>
    /// <exception cref="IndexOutOfRangeException">The matching header index is outside this row.</exception>
    public bool TryGetByHeader(IReadOnlyList<string> headers, string headerName, out string? value)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(headerName);

        var normalizedHeaderName = headerName.Trim();
        var headerIndex = headers
            .Select((header, index) => new { header, index })
            .FirstOrDefault(item => string.Equals(item.header, normalizedHeaderName, StringComparison.OrdinalIgnoreCase))
            ?.index ?? -1;

        if (headerIndex < 0)
        {
            value = null;
            return false;
        }

        value = this[headerIndex];
        return true;
    }

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)_values).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Creates a parsed row, taking ownership of the values.</summary>
    /// <param name="values">The field values.</param>
    /// <param name="lineNumber">The line the record starts on.</param>
    /// <returns>The row.</returns>
    internal static CsvRow FromParsed(string[] values, int lineNumber) => new(values, lineNumber);
}
