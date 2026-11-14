using PandaBear.Results;

namespace PandaBear.Tabular;

/// <summary>
/// Defines common error results for CSV document processing, such as inconsistent column counts across rows.
/// </summary>
internal static class CsvErrors
{
    /// <summary>
    /// Result indicating that a required header is missing.
    /// </summary>
    public static Result RequiredHeaderMissing(string headerName) =>
        Result.Invalid(
            detail: $"Required header '{headerName}' is missing.",
            field: "headers",
            message: "Required header missing."
        );

    /// <summary>
    /// Result indicating that all rows must have the same number of columns as there are headers.
    /// </summary>
    public static Result RowsHaveInconsistentColumnCounts() =>
        Result.Invalid(
            detail: "All rows must have the same number of columns as there are headers.",
            field: "rows",
            message: "Inconsistent column counts detected."
        );

    /// <summary>
    /// Result indicating that rows have inconsistent column counts and includes CSV line metadata.
    /// </summary>
    public static Result RowsHaveInconsistentColumnCounts(int csvLineNumber) =>
        Result.Invalid(
            detail: $"All rows must have the same number of columns as there are headers (line {csvLineNumber}).",
            field: "rows",
            message: "Inconsistent column counts detected."
        );

    /// <summary>
    /// Result indicating that CSV headers must contain at least one column.
    /// </summary>
    public static Result HeadersMustContainAtLeastOneColumn() =>
        Result.Invalid(
            detail: "Headers must contain at least one column.",
            field: "headers",
            message: "Missing headers."
        );

    /// <summary>
    /// Result indicating that header count must match row column count and includes CSV line metadata.
    /// </summary>
    public static Result HeaderCountMustMatchRowColumnCount(int csvLineNumber) =>
        Result.Invalid(
            detail: $"Header count must match row column count (line {csvLineNumber}).",
            field: "headers",
            message: "Header and row column counts do not match."
        );

    /// <summary>
    /// Result indicating that a quoted field is not closed before the end of the content.
    /// </summary>
    public static Result UnterminatedQuotedField(int csvLineNumber) =>
        Result.Invalid(
            detail: $"A quoted field is not closed (line {csvLineNumber}).",
            field: "rows",
            message: "Unterminated quoted field."
        );

    /// <summary>
    /// Result indicating that text follows the closing quote of a quoted field.
    /// </summary>
    public static Result TextAfterQuotedField(int csvLineNumber) =>
        Result.Invalid(
            detail: $"A quoted field is followed by text before the next comma or line break (line {csvLineNumber}).",
            field: "rows",
            message: "Text after a quoted field."
        );
}
