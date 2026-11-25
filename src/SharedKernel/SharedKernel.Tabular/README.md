# PandaBear.SharedKernel.Tabular

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Read and write RFC 4180 CSV, resolve headers, and find later duplicate rows using caller-defined keys. Reading is lenient, so a simple
unquoted file reads as a naive comma split would read it; writing produces what reading gives back exactly.

## Install

Replace the placeholders with an actually validated candidate version and its local/private feed. Registry publication remains deferred.

```text
dotnet add package PandaBear.SharedKernel.Tabular --version <validated-candidate-version> --source <candidate-feed>
```

## Example

```csharp
using SharedKernel.Tabular;

var result = CsvDocument.Parse("Code,Label\nalpha,\"One, first\"\nALPHA,Two", ["Code"]);
if (result.IsSuccess)
{
    var document = result.Value;
    var duplicates = DuplicateRowScanner.FindDuplicateLineNumbers(document,
        row => row[document.Headers, "Code"].ToUpperInvariant());
    // duplicates contains document line 3.

    var csv = CsvWriter.Write(document.Headers, document.Rows);
    // "Code,Label\r\nalpha,\"One, first\"\r\nALPHA,Two\r\n"
}
```

## Reading

`CsvDocument.Parse` (text) and `CsvDocument.ParseUtf8` (bytes) follow the same rules and return the same document or error:

- A leading byte order mark is removed. Records end at LF or CRLF; a final line break adds no record.
- A field whose first character other than spaces and tabs is `"` is quoted: it ends at the matching quote, `""` inside it is one quote,
  and it may hold commas and line breaks. Its content is kept exactly, white space included. Only spaces and tabs may follow the closing
  quote before the next comma or line break; other text is an error.
- Any other field ends at the next comma or line break and is trimmed. A quote inside it, as in `O"Neil` or `ab"c"d`, is a literal
  character, so files written by tools that do not quote read as before.
- A quoted field still open at the end of the content is an error.
- Blank lines (empty or only white space) are skipped by default, before and after the header, because spreadsheets and editors leave
  them and in a multi-column document they could only fail the column count. A quoted empty field (`""`) is a value, not a blank line.
  `new CsvParseOptions { SkipBlankLines = false }` reads every blank line as a record of one empty field.

Required-header checks ignore ordinal case and blank required names; header lookup selects the first match. Structural failures are
invalid Results whose detail names the physical line the record starts on; `CsvRow.LineNumber` is that line, counting skipped blank lines
and the line breaks inside quoted fields. Consumers own accepted headers, normalization, business conflicts, and file creation.

`ParseUtf8` finds fields on the bytes and decodes each field on its own, without decoding the whole content or each line: every
structural character is ASCII, and no byte of a multibyte UTF-8 sequence is. Invalid UTF-8 is replaced per field as the decoder does.

## Mapping source headers

Pass a target schema in display order to `CsvHeaderMapping.Match`. Matching uses ordinal case-insensitive comparison by
default and returns the original spelling of each selected source. Pass a comparer to both operations to change how names
are matched. Supply target-to-source overrides to confirm alternate inputs by name. A null override clears a match.

```csharp
var parsed = CsvDocument.Parse("Sala,Capacity,Notes\nAurora,24,South window");
if (parsed.IsSuccess)
{
    var document = parsed.Value;
    var choices = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Room"] = "Sala" };
    var matches = CsvHeaderMapping.Match(document.Headers, ["Room", "Capacity"], choices);
    var mapped = CsvHeaderMapping.Remap(document, matches);
    // Headers are Room, Capacity, Notes. The row still points to physical line 2 in the source.

    var exact = CsvWriter.Write(mapped.Headers, mapped.Rows, new CsvWriteOptions { GuardFormulas = false });
    var forSpreadsheet = CsvWriter.Write(mapped.Headers, mapped.Rows);
}
```

Remapping keeps source column order and all row values, including columns outside the target schema. Supply a rename policy
when a declined or superseded source header still uses a canonical name and should be relabeled. By default it stays as
written. Renaming retains the parsed rows and their physical line numbers, including skipped blank lines and line breaks
inside quoted fields. If a later step serializes and reparses the data, those new physical line numbers refer to the new file.
Keep the mapped document or carry its row locations separately wherever diagnostics must refer to the original file.

## Writing

`CsvWriter.Write(headers, rows, options)` returns the text, and an overload writes to a `TextWriter`; rows may be any lists of strings,
including the `CsvRow`s of a parsed document. Records end with CRLF (`CsvWriteOptions.NewLine` may be `"\n"`). A value is quoted, with
its quotes doubled, when it holds a comma, quote or line break, starts or ends with white space (which reading would trim), or starts with
a byte order mark; a record of one empty value is written as `""` so it is not read as a blank line. `CsvWriter.FormatField` formats one
value the same way.

The formula-injection guard (`CsvWriteOptions.GuardFormulas`, on by default) writes an apostrophe in front of a value a spreadsheet would
run, inside quotes: one whose first character after white space and control characters is `=`, `+`, `-` or `@` (or their full-width
forms), or whose first character is a control character such as a tab or carriage return. Negative numbers are guarded too.

Round trip: with the guard off, `Parse(Write(headers, rows))` gives back exactly the same headers and rows, with either blank-line option;
with the guard on, every guarded value comes back with its apostrophe and every other value unchanged. Property tests check both.

## Duplicates

Duplicate scanning compares keys ordinally by default or uses the supplied comparer. Null skips a row; empty strings are compared unless
the selector skips them. The first occurrence is retained and the line numbers of later duplicates are returned in order: the parsed
`CsvRow.LineNumber`, or for a row made with `CsvRow.Create` its position after a one-line header (the first data row is line 2).

Package ID: `PandaBear.SharedKernel.Tabular`. C# namespace: `SharedKernel.Tabular`. Assembly/project identity: `SharedKernel.Tabular`. Only the existing Results package is referenced; no CSV dependency is added.
