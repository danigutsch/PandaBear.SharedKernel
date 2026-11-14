namespace PandaBear.Tabular;

/// <summary>Options of <see cref="CsvDocument.Parse"/> and <see cref="CsvDocument.ParseUtf8"/>.</summary>
public sealed record CsvParseOptions
{
    /// <summary>Gets the default options: blank lines are skipped.</summary>
    public static CsvParseOptions Default { get; } = new();

    /// <summary>
    /// Gets a value indicating whether lines that are empty or hold only white space are skipped, before and after the header. Default
    /// <see langword="true"/>: spreadsheets and editors leave blank lines that are never meant as records, and in a document of several
    /// columns such a line could only fail the column count. A quoted empty field (<c>""</c>) is a value, not a blank line, so a
    /// one-column document whose empty values matter stays readable; set it to <see langword="false"/> to read every blank line as a record
    /// of one empty field.
    /// </summary>
    public bool SkipBlankLines { get; init; } = true;
}
