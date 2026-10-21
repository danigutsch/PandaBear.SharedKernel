namespace PandaBear.Tabular;

/// <summary>Represents a row whose comma-separated fields are trimmed.</summary>
/// <remarks>Commas always separate fields; quoted, escaped, and multiline fields are not supported.</remarks>
public sealed class CsvRow
{
    private readonly string[] _values;

    private CsvRow(string[] values)
    {
        _values = values;
    }

    /// <summary>Gets the number of fields, including empty fields.</summary>
    public int Count => _values.Length;

    /// <summary>Gets the field at the zero-based column index.</summary>
    /// <param name="index">The zero-based column index.</param>
    /// <returns>The trimmed field value.</returns>
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

    /// <summary>Splits a line on every comma and trims each field.</summary>
    /// <param name="csvLine">The line to split.</param>
    /// <returns>A row retaining empty fields.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="csvLine"/> is null.</exception>
    public static CsvRow Parse(string csvLine)
    {
        ArgumentNullException.ThrowIfNull(csvLine);

        var values = csvLine.Split(',').Select(value => value.Trim()).ToArray();
        return new CsvRow(values);
    }
}
