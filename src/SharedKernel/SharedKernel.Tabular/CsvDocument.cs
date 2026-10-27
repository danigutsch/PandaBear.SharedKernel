using System.Text;
using PandaBear.Results;

namespace PandaBear.Tabular;

/// <summary>
/// Represents a simple comma-delimited document with headers and data rows.
/// </summary>
/// <remarks>Quoted delimiters, escaping, and multiline fields are not supported. Empty data lines are retained.</remarks>
public sealed class CsvDocument
{
    private readonly IReadOnlyList<CsvRow> _rows;

    private CsvDocument(IEnumerable<string> headers, IEnumerable<CsvRow> rows)
    {
        Headers = [.. headers];
        _rows = [.. rows];
    }

    /// <summary>Gets the trimmed header columns.</summary>
    public IReadOnlyList<string> Headers
    {
        get;
    }

    /// <summary>Gets the data rows aligned with the header columns.</summary>
    public IReadOnlyList<CsvRow> Rows => _rows;

    /// <summary>
    /// Parses CSV content into a document with headers and rows.
    /// </summary>
    /// <param name="content">Full CSV content, where the first line is the header row.</param>
    /// <param name="requiredHeaderNames">Optional list of required headers that must exist in the CSV header row.</param>
    /// <returns>A parsed CSV document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
    public static Result<CsvDocument> Parse(string content, IReadOnlyList<string>? requiredHeaderNames = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var lines = content
            .Split(["\r\n", "\n"], StringSplitOptions.TrimEntries);

        if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
        {
            return CsvErrors.HeadersMustContainAtLeastOneColumn().ConvertError<CsvDocument>();
        }

        return Build(CsvRow.Parse(lines[0]), lines.Skip(1).Select(CsvRow.Parse), requiredHeaderNames);
    }

    /// <summary>
    /// Parses UTF-8 CSV bytes into a document with headers and rows without decoding the whole content to text.
    /// </summary>
    /// <param name="content">Full UTF-8 CSV content, where the first line is the header row. A byte order mark is not removed.</param>
    /// <param name="requiredHeaderNames">Optional list of required headers that must exist in the CSV header row.</param>
    /// <returns>The same document or error as <see cref="Parse(string, IReadOnlyList{string}?)"/> returns for the decoded text.</returns>
    public static Result<CsvDocument> ParseUtf8(ReadOnlySpan<byte> content, IReadOnlyList<string>? requiredHeaderNames = null)
    {
        var rows = new List<CsvRow>();
        CsvRow? headerRow = null;
        var fields = new List<string>();
        var remaining = content;
        while (true)
        {
            var lineEnd = remaining.IndexOf((byte)'\n');
            var line = lineEnd < 0 ? remaining : remaining[..lineEnd];
            if (!line.IsEmpty && line[^1] == (byte)'\r')
            {
                line = line[..^1];
            }

            if (headerRow is null)
            {
                var headerLine = Encoding.UTF8.GetString(line).Trim();
                if (string.IsNullOrWhiteSpace(headerLine))
                {
                    return CsvErrors.HeadersMustContainAtLeastOneColumn().ConvertError<CsvDocument>();
                }

                headerRow = CsvRow.Parse(headerLine);
            }
            else
            {
                rows.Add(ParseRow(line, fields));
            }

            if (lineEnd < 0)
            {
                return Build(headerRow, rows, requiredHeaderNames);
            }

            remaining = remaining[(lineEnd + 1)..];
        }
    }

    private static CsvRow ParseRow(ReadOnlySpan<byte> line, List<string> fields)
    {
        fields.Clear();
        while (true)
        {
            var comma = line.IndexOf((byte)',');
            var field = comma < 0 ? line : line[..comma];
            fields.Add(Encoding.UTF8.GetString(field).Trim());
            if (comma < 0)
            {
                return CsvRow.FromValues([.. fields]);
            }

            line = line[(comma + 1)..];
        }
    }

    private static Result<CsvDocument> Build(CsvRow headerRow, IEnumerable<CsvRow> rows, IReadOnlyList<string>? requiredHeaderNames)
    {
        var headers = Enumerable.Range(0, headerRow.Count).Select(i => headerRow[i]).ToArray();

        var missingRequiredHeader = requiredHeaderNames?
            .Select(requiredHeader => requiredHeader.Trim())
            .Where(requiredHeader => !string.IsNullOrWhiteSpace(requiredHeader))
            .FirstOrDefault(requiredHeader =>
                !headers.Any(header =>
                    string.Equals(header, requiredHeader, StringComparison.OrdinalIgnoreCase)
                )
            );

        if (missingRequiredHeader is not null)
        {
            return CsvErrors.RequiredHeaderMissing(missingRequiredHeader).ConvertError<CsvDocument>();
        }

        return Create(headers, [.. rows]);
    }

    /// <summary>
    /// Creates a <see cref="CsvDocument"/> from explicit headers and data rows.
    /// </summary>
    /// <param name="headers">The CSV header columns.</param>
    /// <param name="rows">The CSV data rows (excluding headers).</param>
    /// <returns>A new instance of CsvDocument containing headers and rows.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="headers"/> or <paramref name="rows"/> is null.
    /// </exception>
    public static Result<CsvDocument> Create(IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        if (headers.Count == 0)
        {
            return CsvErrors.HeadersMustContainAtLeastOneColumn().ConvertError<CsvDocument>();
        }

        var firstRowWithInvalidColumnCount = rows
            .Select((row, index) => new { row, index })
            .FirstOrDefault(item => item.row.Count != headers.Count);

        var rowsHaveInconsistentColumnCounts = rows.Select(row => row.Count).Distinct().Count() > 1;
        if (rowsHaveInconsistentColumnCounts)
        {
            if (firstRowWithInvalidColumnCount is not null)
            {
                var csvLineNumber = firstRowWithInvalidColumnCount.index + 2;
                return CsvErrors.RowsHaveInconsistentColumnCounts(csvLineNumber).ConvertError<CsvDocument>();
            }

            return CsvErrors.RowsHaveInconsistentColumnCounts().ConvertError<CsvDocument>();
        }

        var headerCountDiffersFromRows = firstRowWithInvalidColumnCount is not null;
        if (headerCountDiffersFromRows)
        {
            var csvLineNumber = firstRowWithInvalidColumnCount!.index + 2;
            return CsvErrors.HeaderCountMustMatchRowColumnCount(csvLineNumber).ConvertError<CsvDocument>();
        }

        return Result.Ok(new CsvDocument(headers, rows));
    }
}
