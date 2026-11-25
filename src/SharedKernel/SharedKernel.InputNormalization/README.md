# PandaBear.SharedKernel.InputNormalization

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable input normalization helpers for shared-kernel workflows.

## Usage

Use `StringSanitizer` for trimming/normalization and key generation, `NumericSanitizer.SanitizePrice` for reusable numeric normalization, and `WebAssetUriSanitizer` for root-relative or HTTPS assets (surrounding white space is trimmed; control characters, inner white space, backslashes, lone surrogates, protocol-relative addresses and user information are refused). Use `ControlCharacters.Escape` before writing text from files, processes, or the network to a terminal or a CI log: it replaces control, format, line separator, and paragraph separator characters, and lone surrogates, with `\uXXXX` or `\UXXXXXXXX` escapes and keeps other text, including non-ASCII letters and emoji. `ControlCharacters.EscapeLines` splits multi-line text, such as an exception message that carries process output, and escapes each line, so a command writes it line by line without a line carrying a control character. Use `PrintableText.OneLine` instead
when the text should read as it would display, such as a name in a report: line breaks and tabs become spaces, and every
other control, format, or default-ignorable character, and each lone surrogate, becomes U+FFFD, so the text can neither break
the line, carry a terminal escape sequence, nor reorder the text around it with a bidirectional mark. Joiners and tag
characters stay where they shape an emoji, a Persian word, or an Indic conjunct. `PrintableText.TableCell` also escapes `|` for
a Markdown table cell. Normalization does not replace domain validation, authorization, or storage access checks.

`Sanitize`, `SanitizeNotes` and `NormalizeKey` throw `ArgumentException` when the text is not well-formed UTF-16, such as a lone
surrogate from a truncated or hostile form field, because Unicode normalization refuses it. For untrusted input use `TrySanitize`,
`TrySanitizeNotes` and `TryNormalizeKey`: they return `false` for such text, and otherwise exactly what the throwing methods return, so
the caller can answer a validation error instead of a server error.

`TrySanitizeParagraphs` splits text typed into one box, such as a description, into paragraphs on blank lines. Every Unicode line
terminator ends a line: line feed, carriage return, both together, next line (U+0085), form feed (U+000C), line separator (U+2028) and
paragraph separator (U+2029), so text pasted from a word processor splits the same way. Each paragraph is
sanitized like `Sanitize` (its line breaks become single spaces), empty ones are dropped, and malformed text returns `false`. Joining the
paragraphs with a blank line gives the same paragraphs again, so a stored list round-trips through an editor's text box.

`PostgreSqlIdentifier.IsUnquoted` checks a name that a library writes into SQL text, such as a configured schema or table name:
an ASCII letter or `_`, then ASCII letters, digits, or `_`, at most 63 bytes. PostgreSQL silently truncates a longer name to
NAMEDATALEN-1 (63) bytes, so two long names could select the same object. Only ASCII is accepted, so the character count is the
byte count. The idempotency, integration event, and disposition hold storage options and the event-sourcing schema use it.

## Install and example

The following placeholders are intentional: replace `<validated-candidate-version>` with a candidate that has actually been built and validated, and `<private-candidate-feed>` with its private local feed. The placeholders do not identify a published package version. The code requires the package's target runtime; integration examples assume application-owned host/provider configuration.

```text
dotnet add package PandaBear.SharedKernel.InputNormalization --version <validated-candidate-version> --source <private-candidate-feed>
```

```csharp
using SharedKernel.InputNormalization;

public static class Example
{
    public static string NormalizeName(string name)
        => StringSanitizer.NormalizeKey(name);
}
```

## Package identity and availability

Package ID: `PandaBear.SharedKernel.InputNormalization`. C# namespace: `SharedKernel.InputNormalization`. Assembly/project identity: `SharedKernel.InputNormalization`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

## Raster rendition media types

`RasterImageMediaType.Find` matches JPEG, PNG, WebP, and AVIF MIME types with
ordinal case comparison and surrounding whitespace removal. The result pairs the canonical
media type with its preferred file extension. Callers select the sizes, addresses, and
display ordering for their renditions.
