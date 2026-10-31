using System.Globalization;
using System.Text;

namespace PandaBear.VaultManager.Tool.Formats.Csv;

/// <summary>
/// Reads RFC 4180 CSV text. <c>SharedKernel.Tabular</c> deliberately does not support quoted fields, but vault exports
/// quote commas, quotes, and line breaks inside values, so the tool keeps this small private reader.
/// </summary>
internal static class CsvReader
{
    /// <summary>Parses CSV text into records. A trailing line break does not produce an empty record.</summary>
    /// <param name="text">CSV text, with or without a byte order mark.</param>
    /// <returns>Records in file order.</returns>
    /// <exception cref="InvalidDataException">A quoted field is unterminated or followed by stray characters.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<CsvRecord> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var records = new List<CsvRecord>();
        var fields = new List<string>();
        var field = new StringBuilder();
        int position = text.StartsWith('\uFEFF') ? 1 : 0;
        int line = 1;
        int recordLine = 1;
        bool recordStarted = false;

        while (position < text.Length)
        {
            recordStarted = true;
            if (text[position] == '"' && field.Length == 0)
            {
                int quoteLine = line;
                position++;
                while (true)
                {
                    if (position >= text.Length)
                    {
                        throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture,
                            $"CSV quoted field starting on line {quoteLine} is not terminated."));
                    }

                    char quoted = text[position];
                    if (quoted == '"')
                    {
                        if (position + 1 < text.Length && text[position + 1] == '"')
                        {
                            field.Append('"');
                            position += 2;
                            continue;
                        }

                        position++;
                        break;
                    }

                    if (quoted == '\n' || (quoted == '\r' && (position + 1 >= text.Length || text[position + 1] != '\n')))
                    {
                        line++;
                    }

                    field.Append(quoted);
                    position++;
                }

                if (position < text.Length && text[position] is not (',' or '\r' or '\n'))
                {
                    throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture,
                        $"CSV line {line} has characters after a closing quote."));
                }

                continue;
            }

            char current = text[position];
            if (current == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
                position++;
            }
            else if (current is '\r' or '\n')
            {
                position += current == '\r' && position + 1 < text.Length && text[position + 1] == '\n' ? 2 : 1;
                fields.Add(field.ToString());
                field.Clear();
                records.Add(new CsvRecord(recordLine, [.. fields]));
                fields.Clear();
                line++;
                recordLine = line;
                recordStarted = false;
            }
            else
            {
                field.Append(current);
                position++;
            }
        }

        if (recordStarted)
        {
            fields.Add(field.ToString());
            records.Add(new CsvRecord(recordLine, [.. fields]));
        }

        return records;
    }
}
