using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PandaBear.VaultManager.Tool.IO;
using PandaBear.VaultManager.Tool.Matching;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>
/// Reads and writes the versioned decisions file:
/// <c>{"version":2,"groups":[...],"links":[...],"splits":[...],"findings":[...],"notes":[...]}</c>. The <c>findings</c>
/// and <c>notes</c> sections are optional when reading; a property the format does not define, or a property given twice in one object, is rejected.
/// Choice kinds, finding decisions, and notes decisions are written by name, and item names as typed, with control and format characters
/// escaped. A pending entry, with the kind or decision <c>Pending</c>, marks what still needs a decision and counts as
/// no decision.
/// </summary>
internal static class DecisionsFile
{
    /// <summary>Format version written by this tool, and the only version it reads.</summary>
    public const int CurrentVersion = 2;

    // Item names keep apostrophes and accented letters as typed, so the file stays readable when edited by hand. Relaxed
    // escaping suits a local file that no web page or script embeds. Quotes, backslashes, control characters, and, through
    // EscapeFormatCharacters, format characters stay escaped, so reading the file gives back the same text.
    private static readonly JsonTypeInfo<DecisionsDocument> ReadableDocument = (JsonTypeInfo<DecisionsDocument>)new JsonSerializerOptions(DecisionsJsonContext.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    }.GetTypeInfo(typeof(DecisionsDocument));

    /// <summary>Reads a decisions file.</summary>
    /// <param name="path">Decisions file.</param>
    /// <returns>The decisions, or <see cref="MergeDecisions.None"/> when the file does not exist.</returns>
    /// <exception cref="InvalidDataException">
    /// The file is not a valid decisions file of a supported version. The message starts with the full path of the file
    /// and names the JSON path of the problem, never a value.
    /// </exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
    public static MergeDecisions Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!File.Exists(path))
        {
            return MergeDecisions.None;
        }

        string json = File.ReadAllText(path);
        try
        {
            return Deserialize(json);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException(Path.GetFullPath(path) + ": " + exception.Message, exception);
        }
    }

    /// <summary>
    /// Writes decisions to a file readable and writable only by the current user on Unix, replacing an existing file.
    /// </summary>
    /// <param name="path">Decisions file.</param>
    /// <param name="decisions">Decisions.</param>
    /// <exception cref="IOException">The file cannot be written.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="decisions"/> is <see langword="null"/>.</exception>
    public static void Write(string path, MergeDecisions decisions)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(decisions);

        SecureFile.WriteAllText(path, Serialize(decisions), overwrite: true);
    }

    /// <summary>Serializes decisions.</summary>
    /// <param name="decisions">Decisions.</param>
    /// <returns>Indented JSON with item names as typed, except for escaped control and format characters.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="decisions"/> is <see langword="null"/>.</exception>
    public static string Serialize(MergeDecisions decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);

        var document = new DecisionsDocument(
            CurrentVersion,
            decisions.Groups,
            decisions.Overrides.Links,
            decisions.Overrides.Splits,
            decisions.Findings,
            decisions.Notes.Count > 0 ? decisions.Notes : null);
        return EscapeFormatCharacters(JsonSerializer.Serialize(document, ReadableDocument));
    }

    /// <summary>Parses and validates decisions.</summary>
    /// <param name="json">Decisions file text.</param>
    /// <returns>The decisions, with their pending entries.</returns>
    /// <exception cref="InvalidDataException">
    /// The text is not a valid decisions file of a supported version, for example because it holds a property the
    /// format does not define or a property twice in one object. The message names the JSON path of the problem, never a
    /// value.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is <see langword="null"/>.</exception>
    public static MergeDecisions Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        DecisionsDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(json, DecisionsJsonContext.Default.DecisionsDocument);
        }
        catch (JsonException exception)
        {
            // The parser's message can quote the text around the problem, so only its location is reported.
            long line = (exception.LineNumber ?? 0) + 1;
            throw new InvalidDataException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The decisions file is not valid at {exception.Path ?? "$"} (line {line}). Look there for a misspelled, unknown, or repeated property, a missing value, a value of the wrong type, or broken JSON syntax."),
                exception);
        }

        if (document is null)
        {
            throw new InvalidDataException("The decisions file is empty.");
        }

        if (document.Version != CurrentVersion)
        {
            throw new InvalidDataException($"The decisions file has version {document.Version}; only version {CurrentVersion} is supported.");
        }

        ValidateGroups(document.Groups);
        IReadOnlyList<ItemFindingDecisions> findings = document.Findings ?? [];
        ValidateFindings(findings);
        IReadOnlyList<ItemNotesDecision> notes = document.Notes ?? [];
        ValidateNotes(notes);
        return new MergeDecisions(document.Groups, new MatchOverrides(document.Links, document.Splits)) { Findings = findings, Notes = notes };
    }

    /// <summary>
    /// Escapes the format characters of JSON written with the relaxed encoder, which writes them as they are, and an
    /// editor applies them instead of showing them: a right-to-left override in an item name reverses the text after it,
    /// and a zero-width character is invisible. Each is written as a <c>\u</c> escape instead. JSON text outside strings
    /// is ASCII, so each one is inside a string, where the escape reads back as the same character.
    /// </summary>
    /// <param name="json">JSON text.</param>
    /// <returns>The same JSON with format characters escaped.</returns>
    internal static string EscapeFormatCharacters(string json)
    {
        var escaped = new StringBuilder(json.Length);
        int index = 0;
        while (index < json.Length)
        {
            int length = char.IsSurrogatePair(json, index) ? 2 : 1;
            if (CharUnicodeInfo.GetUnicodeCategory(json, index) == UnicodeCategory.Format)
            {
                foreach (char unit in json.AsSpan(index, length))
                {
                    escaped.Append(CultureInfo.InvariantCulture, $"\\u{(int)unit:X4}");
                }
            }
            else
            {
                escaped.Append(json, index, length);
            }

            index += length;
        }

        return escaped.ToString();
    }

    private static void ValidateGroups(IReadOnlyList<GroupDecision> groups)
    {
        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.GroupKey))
            {
                throw new InvalidDataException("A decisions file group has no group key.");
            }

            foreach (var choice in group.Choices)
            {
                if (string.IsNullOrWhiteSpace(choice.FieldPath))
                {
                    throw new InvalidDataException($"A choice in group {group.GroupKey} has no field path.");
                }

                if (!Enum.IsDefined(choice.Kind))
                {
                    throw new InvalidDataException($"The choice for {choice.FieldPath} in group {group.GroupKey} is not Pending, Member, or Empty.");
                }

                if (choice.Kind == FieldChoiceKind.Member && string.IsNullOrWhiteSpace(choice.OriginKey))
                {
                    throw new InvalidDataException($"The choice for {choice.FieldPath} in group {group.GroupKey} names no member.");
                }
            }
        }
    }

    private static void ValidateFindings(IReadOnlyList<ItemFindingDecisions> findings)
    {
        foreach (var item in findings)
        {
            if (string.IsNullOrWhiteSpace(item.OriginKey))
            {
                throw new InvalidDataException("A decisions file finding entry has no origin key.");
            }

            foreach (var choice in item.Choices)
            {
                if (string.IsNullOrWhiteSpace(choice.FindingId))
                {
                    throw new InvalidDataException($"A finding decision for {item.OriginKey} has no finding ID.");
                }

                if (!Enum.IsDefined(choice.Decision))
                {
                    throw new InvalidDataException($"The decision on finding {choice.FindingId} of {item.OriginKey} is not Pending, Accepted, or Rejected.");
                }
            }
        }
    }

    private static void ValidateNotes(IReadOnlyList<ItemNotesDecision> notes)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in notes)
        {
            if (string.IsNullOrWhiteSpace(item.OriginKey))
            {
                throw new InvalidDataException("A decisions file notes entry has no origin key.");
            }

            if (!Enum.IsDefined(item.Decision))
            {
                throw new InvalidDataException($"The notes decision of {item.OriginKey} is not Pending, Share, Private, or Hidden.");
            }

            if (!seen.Add(item.OriginKey))
            {
                throw new InvalidDataException($"The decisions file has two notes entries for {item.OriginKey}.");
            }
        }
    }
}
