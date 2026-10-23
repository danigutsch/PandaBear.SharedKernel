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

        var headerRow = CsvRow.Parse(lines[0]);
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

        var rows = lines
            .Skip(1)
            .Select(CsvRow.Parse);

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
