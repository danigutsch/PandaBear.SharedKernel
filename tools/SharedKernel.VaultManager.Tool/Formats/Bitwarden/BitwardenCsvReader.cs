using System.Globalization;
using PandaBear.VaultManager.Tool.Formats.Csv;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>
/// Reads Bitwarden CSV exports. Individual exports have <c>folder</c> and <c>favorite</c> columns; organization exports
/// have <c>collections</c> instead. Both have <c>type,name,notes,fields,reprompt,login_uri,login_username,login_password,login_totp</c>
/// and optionally <c>archivedDate</c>.
/// </summary>
internal static class BitwardenCsvReader
{
    /// <summary>Whether a header row is a Bitwarden CSV header.</summary>
    /// <param name="header">First CSV record.</param>
    /// <returns><see langword="true"/> when the required Bitwarden columns are present.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="header"/> is <see langword="null"/>.</exception>
    public static bool IsHeader(IReadOnlyList<string> header)
    {
        ArgumentNullException.ThrowIfNull(header);

        return header.Contains("type") && header.Contains("name") && header.Contains("login_uri")
            && (header.Contains("folder") || header.Contains("collections"));
    }

    /// <summary>Reads a Bitwarden CSV export.</summary>
    /// <param name="text">File contents.</param>
    /// <param name="sourceFile">File name recorded as the item origin.</param>
    /// <returns>Items and warnings.</returns>
    /// <exception cref="InvalidDataException">The file is malformed or has no Bitwarden header.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="sourceFile"/> is <see langword="null"/>.</exception>
    public static VaultReadResult Read(string text, string sourceFile)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(sourceFile);

        IReadOnlyList<CsvRecord> records = CsvReader.Parse(text);
        if (records.Count == 0 || !IsHeader(records[0].Fields))
        {
            throw new InvalidDataException("File is not a Bitwarden CSV export: the header row is missing required columns.");
        }

        IReadOnlyList<string> header = records[0].Fields;
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < header.Count; index++)
        {
            columns.TryAdd(header[index], index);
        }

        var items = new List<VaultItem>();
        foreach (CsvRecord row in records.Skip(1))
        {
            if (row.Fields.Count == 1 && row.Fields[0].Length == 0)
            {
                continue;
            }

            if (row.Fields.Count != header.Count)
            {
                throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture,
                    $"Bitwarden CSV line {row.LineNumber} has {row.Fields.Count} fields; the header has {header.Count}."));
            }

            items.Add(MapRow(row.Fields, columns, sourceFile));
        }

        string[] warnings =
        [
            "Bitwarden CSV exports omit cards, identities, SSH keys, passkeys, password history, field types, and attachments; export JSON for a complete conversion.",
        ];
        return new VaultReadResult(new VaultExport(items), warnings);
    }

    private static VaultItem MapRow(IReadOnlyList<string> row, Dictionary<string, int> columns, string sourceFile)
    {
        string? Cell(string name) => columns.TryGetValue(name, out int index) && row[index].Length > 0 ? row[index] : null;

        var notes = new List<string>();
        string? folder = Cell("folder");
        string[] otherFolders = [];
        if (Cell("collections") is { } collections)
        {
            string[] names = [.. collections.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal)];
            folder = names.FirstOrDefault();
            otherFolders = names.Length > 1 ? names[1..] : [];
            if (names.Length > 1)
            {
                notes.Add(BitwardenJsonReader.CollectionsNote(names.Length));
            }
        }

        string type = Cell("type") ?? "";
        VaultItemType itemType = type switch
        {
            "login" => VaultItemType.Login,
            _ => VaultItemType.SecureNote,
        };
        if (type is not ("login" or "note"))
        {
            notes.Add("Bitwarden CSV item type is not login or note; stored as a secure note.");
        }

        if (Cell("archivedDate") is not null)
        {
            notes.Add("Item was archived in Bitwarden; the archived state is not carried over.");
        }

        return new VaultItem
        {
            Type = itemType,
            Name = Cell("name") ?? "(no name)",
            FolderPath = folder,
            Favorite = Cell("favorite") == "1",
            Reprompt = Cell("reprompt") is { } reprompt && reprompt != "0",
            Notes = Cell("notes")?.ReplaceLineEndings("\n"),
            Fields = AdditionalFolders.Add(ParseFields(Cell("fields")), otherFolders, folder),
            Login = itemType == VaultItemType.Login
                ? new LoginData
                {
                    Username = Cell("login_username"),
                    Password = Cell("login_password"),
                    Totp = Cell("login_totp"),
                    Uris = [.. (Cell("login_uri") ?? "")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(uri => new LoginUri(uri))],
                }
                : null,
            Origin = new ItemOrigin(BitwardenJsonReader.Provider, sourceFile, null, null),
            ConversionNotes = notes,
        };
    }

    private static List<CustomField> ParseFields(string? fields)
    {
        var parsed = new List<CustomField>();
        if (fields is null)
        {
            return parsed;
        }

        foreach (string line in fields.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            int separator = line.LastIndexOf(": ", StringComparison.Ordinal);
            parsed.Add(separator < 0
                ? new CustomField(null, line, CustomFieldType.Text)
                : new CustomField(line[..separator], line[(separator + 2)..], CustomFieldType.Text));
        }

        return parsed;
    }
}
