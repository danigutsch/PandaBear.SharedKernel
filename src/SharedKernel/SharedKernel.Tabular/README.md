# PandaBear.SharedKernel.Tabular

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Parse simple comma-delimited documents, resolve headers, and find later duplicate rows using caller-defined keys. This is not a full CSV implementation: quoted commas, escaping, and multiline fields are unsupported. Every comma separates a field.

## Install

Replace the placeholders with an actually validated candidate version and its local/private feed. Registry publication remains deferred.

```text
dotnet add package PandaBear.SharedKernel.Tabular --version <validated-candidate-version> --source <candidate-feed>
```

## Example

```csharp
using PandaBear.Tabular;

var result = CsvDocument.Parse("Code,Label\nalpha,One\nALPHA,Two", ["Code"]);
if (result.IsSuccess)
{
    var document = result.Value;
    var duplicates = DuplicateRowScanner.FindDuplicateLineNumbers(document,
        row => row[document.Headers, "Code"].ToUpperInvariant());
    // duplicates contains document line 3.
}
```

CRLF and LF split document lines; lines and fields are trimmed. Empty fields and empty/trailing data rows remain present. Required-header checks ignore ordinal case and blank required names; duplicate header lookup selects the first match. Structural failures are Results with header/row details and physical line numbers. Consumers own accepted headers, normalization, business conflicts, and file creation.

`CsvDocument.ParseUtf8` parses UTF-8 bytes with the same rules and errors as `Parse` on the decoded text, without allocating the whole text or the line strings. It does not remove a byte order mark, and invalid UTF-8 is replaced per field as the text decoder does. Use it when the caller already holds the received bytes.

Duplicate scanning compares keys ordinally by default or uses the supplied comparer. Null skips a row; empty strings are compared unless the selector skips them. The first occurrence is retained and later duplicate line numbers are returned in order. Header line 1 means the first data row is line 2.

Package ID: `PandaBear.SharedKernel.Tabular`. C# namespace: `PandaBear.Tabular`. Assembly/project identity: `SharedKernel.Tabular`. Only the existing Results package is referenced; no CSV dependency is added.
