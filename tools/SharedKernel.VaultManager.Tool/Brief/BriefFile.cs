using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PandaBear.VaultManager.Tool.IO;
using PandaBear.VaultManager.Tool.Merge;

namespace PandaBear.VaultManager.Tool.Brief;

/// <summary>
/// Reads and writes the versioned brief file:
/// <c>{"version":1,"inputs":[...],"summary":{...},"groups":[...],"suggestions":[...]}</c>. A property the format does not
/// define, a property given twice in one object, a missing property, and a group, member, or finding of a member listed
/// twice are rejected. Text is written as typed, with control and format characters escaped.
/// </summary>
internal static class BriefFile
{
    /// <summary>Format version written by this tool, and the only version it reads.</summary>
    public const int CurrentVersion = 1;

    // Names, folders, and shared notes keep accented letters and apostrophes as typed, as in the catalog.
    private static readonly JsonTypeInfo<BriefDocument> ReadableDocument = (JsonTypeInfo<BriefDocument>)new JsonSerializerOptions(BriefJsonContext.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    }.GetTypeInfo(typeof(BriefDocument));

    /// <summary>Reads a brief file.</summary>
    /// <param name="path">Brief file.</param>
    /// <returns>The brief.</returns>
    /// <exception cref="InvalidDataException">
    /// The file is not a valid brief of a supported version. The message starts with the full path of the file and names
    /// the JSON path of the problem, never a value.
    /// </exception>
    /// <exception cref="IOException">The file does not exist or cannot be read.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    public static BriefDocument Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

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

    /// <summary>Writes a brief to a file readable and writable only by the current user on Unix, replacing an existing file.</summary>
    /// <param name="path">Brief file.</param>
    /// <param name="brief">Brief.</param>
    /// <param name="ct">Observed before the written file is moved into place.</param>
    /// <exception cref="IOException">The file cannot be written.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="brief"/> is <see langword="null"/>.</exception>
    public static void Write(string path, BriefDocument brief, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(brief);

        SecureFile.WriteAllText(path, Serialize(brief), overwrite: true, ct);
    }

    /// <summary>Serializes a brief.</summary>
    /// <param name="brief">Brief.</param>
    /// <returns>Indented JSON with text as typed, except for escaped control and format characters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="brief"/> is <see langword="null"/>.</exception>
    public static string Serialize(BriefDocument brief)
    {
        ArgumentNullException.ThrowIfNull(brief);

        return DecisionsFile.EscapeFormatCharacters(JsonSerializer.Serialize(brief, ReadableDocument));
    }

    /// <summary>Parses and validates a brief.</summary>
    /// <param name="json">Brief file text.</param>
    /// <returns>The brief.</returns>
    /// <exception cref="InvalidDataException">
    /// The text is not a valid brief of a supported version. The message names the JSON path of the problem, never a value.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    public static BriefDocument Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        BriefDocument? brief;
        try
        {
            brief = JsonSerializer.Deserialize(json, BriefJsonContext.Default.BriefDocument);
        }
        catch (JsonException exception)
        {
            // The parser's message can quote the text around the problem, so only its location is reported.
            long line = (exception.LineNumber ?? 0) + 1;
            throw new InvalidDataException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The brief is not valid at {exception.Path ?? "$"} (line {line}). Look there for a misspelled, unknown, missing, or repeated property, a value of the wrong type, or broken JSON syntax."),
                exception);
        }

        if (brief is null)
        {
            throw new InvalidDataException("The brief is empty.");
        }

        if (brief.Version != CurrentVersion)
        {
            throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"The brief has version {brief.Version}; only version {CurrentVersion} is supported."));
        }

        CheckUnique(brief);
        return brief;
    }

    // Each group, member, and finding of a member appears once, so every check finds one entry for it.
    private static void CheckUnique(BriefDocument brief)
    {
        var groups = new HashSet<string>(StringComparer.Ordinal);
        var members = new HashSet<string>(StringComparer.Ordinal);
        foreach (BriefGroup group in brief.Groups)
        {
            if (!groups.Add(group.Key))
            {
                throw new InvalidDataException($"The brief has two groups with the key {group.Key}.");
            }

            foreach (BriefMember member in group.Members)
            {
                if (!members.Add(member.OriginKey))
                {
                    throw new InvalidDataException($"The brief lists {member.OriginKey} twice.");
                }

                var findings = new HashSet<string>(StringComparer.Ordinal);
                foreach (BriefFinding finding in member.Findings.Where(finding => !findings.Add(finding.Id)))
                {
                    throw new InvalidDataException($"The brief lists finding {finding.Id} of {member.OriginKey} twice.");
                }
            }
        }
    }
}
