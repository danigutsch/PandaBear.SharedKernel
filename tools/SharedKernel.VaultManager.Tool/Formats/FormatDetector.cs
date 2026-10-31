using System.Text.Json;
using PandaBear.VaultManager.Tool.Attachments;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Formats.Csv;

namespace PandaBear.VaultManager.Tool.Formats;

/// <summary>Detects vault export formats from file content; file extensions are ignored.</summary>
internal static class FormatDetector
{
    /// <summary>Detects the format of a file.</summary>
    /// <param name="path">Export file.</param>
    /// <returns>Detected format, or <see cref="VaultFileFormat.Unknown"/>.</returns>
    /// <exception cref="InvalidDataException">The file is larger than <see cref="VaultFileReader.MaxInputBytes"/> or is an unsafe archive.</exception>
    public static VaultFileFormat Detect(string path) => Detect(VaultFileReader.ReadCapped(path));

    /// <summary>Detects the format of file content.</summary>
    /// <param name="content">File bytes.</param>
    /// <returns>Detected format, or <see cref="VaultFileFormat.Unknown"/>.</returns>
    /// <exception cref="InvalidDataException">The content is a zip archive that breaks a safety limit.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
    public static VaultFileFormat Detect(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.AsSpan().StartsWith("PK\x03\x04"u8))
        {
            using SafeArchive archive = SafeArchive.Open(new MemoryStream(content, writable: false), ArchiveLimits.Default);
            return archive.Find("export.json") is not null ? VaultFileFormat.KeeperJsonZip
                : archive.Find("data.json") is not null ? VaultFileFormat.BitwardenZip
                : VaultFileFormat.Unknown;
        }

        string text;
        try
        {
            text = VaultFileReader.DecodeText(content);
        }
        catch (InvalidDataException)
        {
            return VaultFileFormat.Unknown;
        }

        string trimmed = text.TrimStart();
        if (trimmed.StartsWith('['))
        {
            return VaultFileFormat.KeeperJson;
        }

        return trimmed.StartsWith('{') ? DetectJson(text) : DetectCsv(text);
    }

    private static VaultFileFormat DetectJson(string text)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        bool encrypted = false;
        bool passwordProtected = false;
        try
        {
            var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(text), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });
            reader.Read();
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                string name = reader.GetString()!;
                names.Add(name);
                reader.Read();
                if (name == "encrypted")
                {
                    encrypted = reader.TokenType == JsonTokenType.True;
                }
                else if (name == "passwordProtected")
                {
                    passwordProtected = reader.TokenType == JsonTokenType.True;
                }

                reader.Skip();
            }
        }
        catch (JsonException)
        {
            return VaultFileFormat.Unknown;
        }

        if (encrypted)
        {
            return passwordProtected ? VaultFileFormat.BitwardenJsonPasswordProtected : VaultFileFormat.BitwardenJsonAccountRestricted;
        }

        if (names.Contains("items"))
        {
            return VaultFileFormat.BitwardenJson;
        }

        return names.Contains("records") || names.Contains("shared_folders") ? VaultFileFormat.KeeperJson : VaultFileFormat.Unknown;
    }

    private static VaultFileFormat DetectCsv(string text)
    {
        IReadOnlyList<CsvRecord> records;
        try
        {
            records = CsvReader.Parse(text);
        }
        catch (InvalidDataException)
        {
            return VaultFileFormat.Unknown;
        }

        if (records.Count == 0)
        {
            return VaultFileFormat.Unknown;
        }

        if (BitwardenCsvReader.IsHeader(records[0].Fields))
        {
            return VaultFileFormat.BitwardenCsv;
        }

        // Keeper CSV has no header; every record has at least the six fixed columns.
        return records.All(record => record.Fields.Count >= 6 || (record.Fields.Count == 1 && record.Fields[0].Length == 0))
            ? VaultFileFormat.KeeperCsv
            : VaultFileFormat.Unknown;
    }
}
