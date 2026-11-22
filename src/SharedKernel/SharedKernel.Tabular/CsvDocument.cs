using PandaBear.Results;

namespace PandaBear.Tabular;

/// <summary>
/// A CSV document: a header record and data records with one field per header. It reads RFC 4180, leniently, so simple unquoted files
/// read as they always did: a quoted field may hold commas, line breaks and doubled quotes and keeps its white space; an unquoted field is
/// trimmed and keeps a quote inside it literally. <see cref="CsvWriter"/> writes what this reads.
/// </summary>
public sealed class CsvDocument
{
    private readonly IReadOnlyList<CsvRow> _rows;

    private CsvDocument(IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows)
    {
        Headers = headers;
        _rows = rows;
    }

    /// <summary>Gets the header columns.</summary>
    public IReadOnlyList<string> Headers { get; }

    /// <summary>Gets the data rows aligned with the header columns.</summary>
    public IReadOnlyList<CsvRow> Rows => _rows;

    /// <summary>
    /// Parses CSV text into a document with headers and rows. A leading byte order mark is removed; records end at LF or CRLF, and a final
    /// line break adds no record. A field whose first character other than spaces and tabs is a quote is quoted: it ends at the matching
    /// quote, <c>""</c> inside it is one quote, and only spaces and tabs may follow it before the next comma or line break. Any other field
    /// ends at the next comma or line break, is trimmed, and keeps a stray quote as a literal character.
    /// </summary>
    /// <param name="content">Full CSV content, where the first record is the header row.</param>
    /// <param name="requiredHeaderNames">Optional list of required headers that must exist in the CSV header row.</param>
    /// <param name="options">The options; <see cref="CsvParseOptions.Default"/> when <see langword="null"/>.</param>
    /// <returns>
    /// The document, or an invalid result for no header, a missing required header, a row whose field count differs, an unterminated quoted
    /// field or text after a closing quote; line numbers in the errors are the physical lines records start on.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
    public static Result<CsvDocument> Parse(string content, IReadOnlyList<string>? requiredHeaderNames = null, CsvParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        return CsvReader<char, Utf16CsvSymbols>.Read(content, requiredHeaderNames, options ?? CsvParseOptions.Default);
    }

    /// <summary>
    /// Parses UTF-8 CSV bytes with the same rules and errors as <see cref="Parse"/> on the decoded text, without decoding the whole content:
    /// fields are found on the bytes and each one is decoded on its own. A UTF-8 byte order mark is removed; invalid UTF-8 is replaced per
    /// field as the decoder does.
    /// </summary>
    /// <param name="content">Full UTF-8 CSV content, where the first record is the header row.</param>
    /// <param name="requiredHeaderNames">Optional list of required headers that must exist in the CSV header row.</param>
    /// <param name="options">The options; <see cref="CsvParseOptions.Default"/> when <see langword="null"/>.</param>
    /// <returns>The same document or error as <see cref="Parse"/> returns for the decoded text.</returns>
    public static Result<CsvDocument> ParseUtf8(ReadOnlySpan<byte> content, IReadOnlyList<string>? requiredHeaderNames = null, CsvParseOptions? options = null) =>
        CsvReader<byte, Utf8CsvSymbols>.Read(content, requiredHeaderNames, options ?? CsvParseOptions.Default);

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
                var csvLineNumber = LineNumber(firstRowWithInvalidColumnCount.row, firstRowWithInvalidColumnCount.index);
                return CsvErrors.RowsHaveInconsistentColumnCounts(csvLineNumber).ConvertError<CsvDocument>();
            }

            return CsvErrors.RowsHaveInconsistentColumnCounts().ConvertError<CsvDocument>();
        }

        var headerCountDiffersFromRows = firstRowWithInvalidColumnCount is not null;
        if (headerCountDiffersFromRows)
        {
            var csvLineNumber = LineNumber(firstRowWithInvalidColumnCount!.row, firstRowWithInvalidColumnCount.index);
            return CsvErrors.HeaderCountMustMatchRowColumnCount(csvLineNumber).ConvertError<CsvDocument>();
        }

        return Result.Ok(new CsvDocument([.. headers], [.. rows]));
    }

    /// <summary>Replaces one header per column while retaining the source rows.</summary>
    /// <param name="headers">The replacement headers in column order.</param>
    /// <returns>The document with the new headers.</returns>
    internal CsvDocument WithHeaders(IReadOnlyList<string> headers) => new([.. headers], _rows);

    /// <summary>Checks the required headers and the column counts of parsed records.</summary>
    /// <param name="headerRow">The header record.</param>
    /// <param name="rows">The data records.</param>
    /// <param name="requiredHeaderNames">The required headers.</param>
    /// <returns>The document or the first error.</returns>
    internal static Result<CsvDocument> Build(CsvRow headerRow, IReadOnlyList<CsvRow> rows, IReadOnlyList<string>? requiredHeaderNames)
    {
        string[] headers = [.. headerRow];

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

        return Create(headers, rows);
    }

    /// <summary>The line a row starts on: its parsed line, or for a created row its position after a one-line header.</summary>
    private static int LineNumber(CsvRow row, int index) => row.LineNumber > 0 ? row.LineNumber : index + 2;
}
