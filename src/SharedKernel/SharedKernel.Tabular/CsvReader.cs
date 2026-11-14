using PandaBear.Results;

namespace PandaBear.Tabular;

/// <summary>
/// Reads RFC 4180 CSV, leniently: a field whose first character other than spaces and tabs is a quote is quoted, may hold commas, line
/// breaks and doubled quotes, and is kept as it is; any other field ends at the next comma or line break, may hold quotes literally, and is
/// trimmed. Records end at LF or CRLF; a final line break adds no record. One implementation serves UTF-16 text and UTF-8 bytes.
/// </summary>
/// <typeparam name="T">The code unit.</typeparam>
/// <typeparam name="TSymbols">The symbols of the encoding.</typeparam>
internal static class CsvReader<T, TSymbols>
    where T : unmanaged, IEquatable<T>
    where TSymbols : struct, ICsvSymbols<T>
{
    /// <summary>Reads a whole document.</summary>
    /// <param name="content">The content, optionally starting with a byte order mark.</param>
    /// <param name="requiredHeaderNames">The required headers.</param>
    /// <param name="options">The options.</param>
    /// <returns>The document, or the first structural error.</returns>
    internal static Result<CsvDocument> Read(ReadOnlySpan<T> content, IReadOnlyList<string>? requiredHeaderNames, CsvParseOptions options)
    {
        if (content.StartsWith(TSymbols.ByteOrderMark))
        {
            content = content[TSymbols.ByteOrderMark.Length..];
        }

        var position = 0;
        var line = 1;
        var fields = new List<string>();
        CsvRow? header = null;
        var rows = new List<CsvRow>();
        while (position < content.Length)
        {
            var startLine = line;
            var outcome = ReadRecord(content, ref position, ref line, fields, out var blank);
            if (outcome is RecordOutcome.Unterminated)
            {
                return CsvErrors.UnterminatedQuotedField(startLine).ConvertError<CsvDocument>();
            }

            if (outcome is RecordOutcome.TextAfterQuote)
            {
                return CsvErrors.TextAfterQuotedField(startLine).ConvertError<CsvDocument>();
            }

            if (blank && (options.SkipBlankLines || header is null))
            {
                if (!options.SkipBlankLines)
                {
                    return CsvErrors.HeadersMustContainAtLeastOneColumn().ConvertError<CsvDocument>();
                }

                continue;
            }

            var row = CsvRow.FromParsed([.. fields], startLine);
            if (header is null)
            {
                header = row;
            }
            else
            {
                rows.Add(row);
            }
        }

        return header is null
            ? CsvErrors.HeadersMustContainAtLeastOneColumn().ConvertError<CsvDocument>()
            : CsvDocument.Build(header, rows, requiredHeaderNames);
    }

    private static RecordOutcome ReadRecord(ReadOnlySpan<T> content, ref int position, ref int line, List<string> fields, out bool blank)
    {
        fields.Clear();
        blank = false;
        var anyQuoted = false;
        while (true)
        {
            var start = position;
            var first = start;
            while (first < content.Length && IsBlank(content[first]))
            {
                first++;
            }

            if (first < content.Length && content[first].Equals(TSymbols.Quote))
            {
                anyQuoted = true;
                var outcome = ReadQuoted(content, first, ref position, ref line, fields);
                if (outcome is not RecordOutcome.Field)
                {
                    return outcome;
                }

                continue;
            }

            var end = start;
            while (end < content.Length && !content[end].Equals(TSymbols.Comma) && !content[end].Equals(TSymbols.LineFeed))
            {
                end++;
            }

            var raw = content[start..end];
            var endsRecord = end >= content.Length || content[end].Equals(TSymbols.LineFeed);
            if (endsRecord && !raw.IsEmpty && raw[^1].Equals(TSymbols.CarriageReturn))
            {
                raw = raw[..^1];
            }

            fields.Add(TSymbols.Decode(raw).Trim());
            if (end >= content.Length)
            {
                position = end;
                break;
            }

            position = end + 1;
            if (endsRecord)
            {
                line++;
                break;
            }
        }

        blank = !anyQuoted && fields is [{ Length: 0 }];
        return RecordOutcome.Record;
    }

    /// <summary>Reads a quoted field whose opening quote is at <paramref name="quote"/>, and what may follow it.</summary>
    /// <returns><see cref="RecordOutcome.Field"/> when a comma follows, <see cref="RecordOutcome.Record"/> when the record ends.</returns>
    private static RecordOutcome ReadQuoted(ReadOnlySpan<T> content, int quote, ref int position, ref int line, List<string> fields)
    {
        var index = quote + 1;
        var escaped = false;
        while (true)
        {
            if (index >= content.Length)
            {
                return RecordOutcome.Unterminated;
            }

            var current = content[index];
            if (current.Equals(TSymbols.Quote))
            {
                if (index + 1 < content.Length && content[index + 1].Equals(TSymbols.Quote))
                {
                    escaped = true;
                    index += 2;
                    continue;
                }

                break;
            }

            if (current.Equals(TSymbols.LineFeed))
            {
                line++;
            }

            index++;
        }

        var value = TSymbols.Decode(content[(quote + 1)..index]);
        fields.Add(escaped ? value.Replace("\"\"", "\"", StringComparison.Ordinal) : value);
        index++;
        while (index < content.Length && IsBlank(content[index]))
        {
            index++;
        }

        if (index >= content.Length || (index + 1 == content.Length && content[index].Equals(TSymbols.CarriageReturn)))
        {
            position = content.Length;
            return RecordOutcome.Record;
        }

        if (content[index].Equals(TSymbols.Comma))
        {
            position = index + 1;
            return RecordOutcome.Field;
        }

        var lineFeed = content[index].Equals(TSymbols.CarriageReturn) && index + 1 < content.Length ? index + 1 : index;
        if (content[lineFeed].Equals(TSymbols.LineFeed))
        {
            position = lineFeed + 1;
            line++;
            return RecordOutcome.Record;
        }

        return RecordOutcome.TextAfterQuote;
    }

    private static bool IsBlank(T value) => value.Equals(TSymbols.Space) || value.Equals(TSymbols.Tab);

    private enum RecordOutcome
    {
        Record,
        Field,
        Unterminated,
        TextAfterQuote,
    }
}
