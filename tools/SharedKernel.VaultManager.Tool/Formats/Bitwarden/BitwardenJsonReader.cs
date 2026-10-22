using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Reads unencrypted Bitwarden JSON exports, individual or organization.</summary>
internal static class BitwardenJsonReader
{
    /// <summary>Provider name recorded in <see cref="ItemOrigin.Provider"/>.</summary>
    public const string Provider = "bitwarden";

    /// <summary>Reads an unencrypted Bitwarden JSON export.</summary>
    /// <param name="json">Export file contents. Decrypt password-protected files first.</param>
    /// <param name="sourceFile">File name recorded as the item origin.</param>
    /// <returns>Items in the export, excluding deleted items.</returns>
    /// <exception cref="InvalidDataException">The file is not an unencrypted Bitwarden JSON export.</exception>
    public static VaultExport Read(string json, string sourceFile)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(sourceFile);

        BitwardenExportDto? export;
        try
        {
            export = JsonSerializer.Deserialize(json, BitwardenJsonContext.Default.BitwardenExportDto);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Bitwarden export is not valid JSON.", exception);
        }

        if (export?.Items is null)
        {
            throw new InvalidDataException("File is not a Bitwarden JSON export: it has no items array.");
        }

        if (export.Encrypted)
        {
            throw new InvalidDataException(
                "This Bitwarden export is account-restricted and can only be decrypted by the Bitwarden account that created it. "
                + "Export again as .json or as password-protected .json (Encrypted).");
        }

        Dictionary<string, string> folders = (export.Folders ?? export.Collections ?? [])
            .Where(folder => folder.Id is not null && !string.IsNullOrWhiteSpace(folder.Name))
            .ToDictionary(folder => folder.Id!, folder => folder.Name!, StringComparer.Ordinal);

        return new VaultExport([.. export.Items.Where(item => item.DeletedDate is null).Select(item => Map(item, folders, sourceFile))]);
    }

    private static VaultItem Map(BitwardenItemDto item, Dictionary<string, string> folders, string sourceFile)
    {
        var notes = new List<string>();
        string? folderPath = ResolveFolder(item, folders, notes);
        bool known = Enum.IsDefined((VaultItemType)item.Type);
        var fields = new List<CustomField>(item.Fields ?? []);
        if (!known)
        {
            fields.AddRange(FlattenUnknownPayload(item.Extra));
            notes.Add(string.Create(CultureInfo.InvariantCulture,
                $"Bitwarden item type {item.Type} is not supported by default Vaultwarden imports; stored as a secure note with fields."));
        }

        VaultItemType type = known ? (VaultItemType)item.Type : VaultItemType.SecureNote;
        return new VaultItem
        {
            Type = type,
            Name = string.IsNullOrWhiteSpace(item.Name) ? "(no name)" : item.Name,
            FolderPath = folderPath,
            Favorite = item.Favorite,
            Reprompt = item.Reprompt != 0,
            Notes = item.Notes,
            Fields = fields,
            Login = type == VaultItemType.Login ? MapLogin(item.Login) : null,
            Card = type == VaultItemType.Card ? item.Card ?? new CardData() : null,
            Identity = type == VaultItemType.Identity ? item.Identity ?? new IdentityData() : null,
            SshKey = type == VaultItemType.SshKey ? item.SshKey ?? new SshKeyData() : null,
            PasswordHistory = [.. (item.PasswordHistory ?? []).Where(entry => !string.IsNullOrEmpty(entry.Password))
                .OrderByDescending(entry => entry.LastUsedDate)],
            CreationDate = item.CreationDate,
            RevisionDate = item.RevisionDate,
            Origin = new ItemOrigin(Provider, sourceFile, item.Id, null),
            ConversionNotes = notes,
        };
    }

    private static string? ResolveFolder(BitwardenItemDto item, Dictionary<string, string> folders, List<string> notes)
    {
        if (item.FolderId is not null)
        {
            return folders.GetValueOrDefault(item.FolderId);
        }

        IReadOnlyList<string> collections = item.CollectionIds ?? [];
        if (collections.Count > 1)
        {
            notes.Add("Item belonged to several collections; the first one became its folder.");
        }

        return collections.Count > 0 ? folders.GetValueOrDefault(collections[0]) : null;
    }

    private static LoginData MapLogin(BitwardenLoginDto? login)
    {
        if (login is null)
        {
            return new LoginData();
        }

        return new LoginData
        {
            Username = login.Username,
            Password = login.Password,
            Totp = login.Totp,
            Uris = [.. (login.Uris ?? [])
                .Where(uri => !string.IsNullOrWhiteSpace(uri.Uri))
                .Select(uri => new LoginUri(uri.Uri!, uri.Match is { } match && Enum.IsDefined((UriMatch)match) ? (UriMatch)match : null))],
            Fido2Credentials = login.Fido2Credentials ?? [],
        };
    }

    private static IEnumerable<CustomField> FlattenUnknownPayload(JsonObject? extra)
    {
        if (extra is null)
        {
            yield break;
        }

        foreach ((string _, JsonNode? payload) in extra)
        {
            if (payload is not JsonObject properties)
            {
                continue;
            }

            foreach ((string name, JsonNode? value) in properties)
            {
                if (value is JsonValue scalar && scalar.ToString() is { Length: > 0 } text)
                {
                    yield return new CustomField(name, text, CustomFieldType.Hidden);
                }
            }
        }
    }
}
