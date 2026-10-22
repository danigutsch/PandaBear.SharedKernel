using System.Globalization;
using System.Text.Json;
using PandaBear.VaultManager.Tool.Attachments;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Formats.Keeper;

/// <summary>Reads Keeper JSON exports from the web vault or Commander, including Commander zips with attachments.</summary>
internal static class KeeperJsonReader
{
    private const string ExportEntry = "export.json";
    private const string FilesPrefix = "files/";

    /// <summary>Reads a Keeper JSON export: an object with <c>shared_folders</c> and <c>records</c>, or a bare record array.</summary>
    /// <param name="json">Export file contents.</param>
    /// <param name="sourceFile">File name recorded as the item origin.</param>
    /// <returns>Items and warnings.</returns>
    /// <exception cref="InvalidDataException">The file is not a Keeper JSON export.</exception>
    public static VaultReadResult Read(string json, string sourceFile)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(sourceFile);

        return Read(json, sourceFile, zipFiles: null, []);
    }

    /// <summary>Reads a Commander <c>export --format json --zip</c> archive.</summary>
    /// <param name="path">Zip file holding <c>export.json</c> and <c>files/&lt;file_uid&gt;</c> entries.</param>
    /// <returns>Items with attachment references, and warnings.</returns>
    /// <exception cref="InvalidDataException">The archive breaks a safety limit or holds no Keeper export.</exception>
    public static VaultReadResult ReadZip(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        using SafeArchive archive = SafeArchive.Open(path);
        ArchiveEntryInfo export = archive.Find(ExportEntry)
            ?? throw new InvalidDataException("Keeper zip export has no export.json entry.");
        var zipFiles = archive.Entries
            .Where(entry => entry.Path.StartsWith(FilesPrefix, StringComparison.Ordinal))
            .ToDictionary(entry => entry.Path, entry => entry.Size, StringComparer.Ordinal);
        return Read(archive.ReadUtf8(export), Path.GetFileName(path), zipFiles, []);
    }

    private static VaultReadResult Read(string json, string sourceFile, Dictionary<string, long>? zipFiles, List<string> warnings)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Keeper export is not valid JSON.", exception);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            JsonElement records;
            if (root.ValueKind == JsonValueKind.Array)
            {
                records = root;
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                if (!root.TryGetProperty("records", out records))
                {
                    records = default;
                }
                else if (records.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException("Keeper export property \"records\" is not an array.");
                }

                WarnAboutSharing(root, warnings);
            }
            else
            {
                throw new InvalidDataException("File is not a Keeper JSON export.");
            }

            var items = new List<VaultItem>();
            if (records.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (JsonElement record in records.EnumerateArray())
                {
                    if (record.ValueKind == JsonValueKind.Object)
                    {
                        items.Add(KeeperRecordMapper.Map(ParseRecord(record), sourceFile, zipFiles, warnings));
                    }
                    else
                    {
                        warnings.Add(string.Create(CultureInfo.InvariantCulture, $"Keeper record {index} is not an object and was skipped."));
                    }

                    index++;
                }
            }

            return new VaultReadResult(new VaultExport(items), warnings);
        }
    }

    private static void WarnAboutSharing(JsonElement root, List<string> warnings)
    {
        if (root.TryGetProperty("shared_folders", out JsonElement sharedFolders) && sharedFolders.ValueKind == JsonValueKind.Array)
        {
            int withPermissions = sharedFolders.EnumerateArray().Count(folder =>
                folder.ValueKind == JsonValueKind.Object && folder.TryGetProperty("permissions", out _));
            if (withPermissions > 0)
            {
                warnings.Add(string.Create(CultureInfo.InvariantCulture,
                    $"Keeper shared folder permissions and members are not carried over ({withPermissions} shared folder(s))."));
            }
        }

        if (root.TryGetProperty("teams", out _))
        {
            warnings.Add("Keeper teams are not carried over.");
        }
    }

    private static KeeperRecord ParseRecord(JsonElement record)
    {
        var fields = new List<KeeperField>();
        if (record.TryGetProperty("custom_fields", out JsonElement customFields) && customFields.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in customFields.EnumerateObject())
            {
                (string type, string label) = KeeperFieldKey.Parse(property.Name);
                fields.Add(new KeeperField(type, label, property.Value.Clone()));
            }
        }

        return new KeeperRecord
        {
            Uid = Scalar(record, "uid"),
            Type = Scalar(record, "$type"),
            Title = Scalar(record, "title"),
            Login = Scalar(record, "login"),
            Password = Scalar(record, "password"),
            LoginUrl = Scalar(record, "login_url"),
            Notes = Scalar(record, "notes"),
            LastModified = record.TryGetProperty("last_modified", out JsonElement modified) && modified.TryGetInt64(out long seconds) && seconds > 0
                ? seconds
                : null,
            Fields = fields,
            Folders = [.. Objects(record, "folders").Select(folder => new KeeperFolder(
                Scalar(folder, "shared_folder"),
                Scalar(folder, "folder"),
                Flag(folder, "can_edit"),
                Flag(folder, "can_share")))],
            Attachments = [.. Objects(record, "attachments").Select(attachment => new KeeperAttachment(
                Scalar(attachment, "file_uid"),
                Scalar(attachment, "name") ?? Scalar(attachment, "file_uid") ?? "attachment"))],
            ReferenceCount = CountReferences(record),
            HasSchema = record.TryGetProperty("schema", out JsonElement schema) && schema.ValueKind == JsonValueKind.Array && schema.GetArrayLength() > 0,
        };
    }

    private static int CountReferences(JsonElement record)
    {
        if (!record.TryGetProperty("references", out JsonElement references) || references.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return references.EnumerateObject().Sum(reference => reference.Value.ValueKind switch
        {
            JsonValueKind.Array => reference.Value.GetArrayLength(),
            JsonValueKind.String => 1,
            _ => 0,
        });
    }

    private static IEnumerable<JsonElement> Objects(JsonElement record, string name) =>
        record.TryGetProperty(name, out JsonElement array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object)
            : [];

    private static string? Scalar(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
}
