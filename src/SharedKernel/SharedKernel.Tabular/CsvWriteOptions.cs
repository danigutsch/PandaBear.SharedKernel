namespace SharedKernel.Tabular;

/// <summary>Options of <see cref="CsvWriter"/>.</summary>
public sealed record CsvWriteOptions
{
    /// <summary>Gets the default options: formulas guarded, CRLF line breaks.</summary>
    public static CsvWriteOptions Default { get; } = new();

    /// <summary>
    /// Gets a value indicating whether a value a spreadsheet would run as a formula is written with an apostrophe in front, inside quotes:
    /// one whose first character, after white space and control characters, is <c>=</c>, <c>+</c>, <c>-</c> or <c>@</c> (or their
    /// full-width forms), or whose first character is a control character such as a tab or carriage return. Default <see langword="true"/>.
    /// Reading the file back gives the value with the apostrophe; turn it off for files that only programs read.
    /// </summary>
    public bool GuardFormulas { get; init; } = true;

    /// <summary>Gets the line break after each record: <c>"\r\n"</c> (RFC 4180, the default) or <c>"\n"</c>.</summary>
    /// <exception cref="ArgumentException">The value is neither <c>"\r\n"</c> nor <c>"\n"</c>.</exception>
    public string NewLine
    {
        get;
        init
        {
            if (value is not ("\r\n" or "\n"))
            {
                throw new ArgumentException("The line break must be \"\\r\\n\" or \"\\n\".", nameof(value));
            }

            field = value;
        }
    } = "\r\n";
}
