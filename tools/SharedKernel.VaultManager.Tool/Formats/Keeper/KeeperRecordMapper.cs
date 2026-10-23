using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Formats.Keeper;

/// <summary>
/// Maps Keeper records to vault items. Field names, value formats, and item types follow Bitwarden's Keeper importer
/// where practical, so a record imported into Bitwarden and never edited compares equal. Every value lands in a typed
/// slot or a custom field.
/// </summary>
internal static class KeeperRecordMapper
{
    /// <summary>Provider name recorded in <see cref="ItemOrigin.Provider"/>.</summary>
    public const string Provider = "keeper";

    private static readonly HashSet<string> IdentityTypes = new(StringComparer.Ordinal)
    {
        "contact", "address", "driverLicense", "passport", "ssnCard", "birthCertificate",
    };

    private static readonly HashSet<string> HiddenTypes = new(StringComparer.Ordinal)
    {
        "password", "secret", "pinCode", "oneTimeCode", "licenseNumber", "accountNumber", "bankAccount", "paymentCard",
        "keyPair", "privateKey", "passkey",
    };

    private static readonly HashSet<string> DateTypes = new(StringComparer.Ordinal) { "date", "birthDate", "expirationDate" };

    private static readonly JsonWriterOptions CompactJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Maps one Keeper record.</summary>
    /// <param name="record">Record read from the export.</param>
    /// <param name="sourceFile">File name recorded as the item origin.</param>
    /// <param name="zipFiles">Sizes of <c>files/&lt;file_uid&gt;</c> entries of a Commander zip, or <see langword="null"/> for plain JSON or CSV.</param>
    /// <param name="warnings">Receives problems; warnings name items and fields, never values.</param>
    /// <returns>Vault item.</returns>
    public static VaultItem Map(KeeperRecord record, string sourceFile, IReadOnlyDictionary<string, long>? zipFiles, ICollection<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(warnings);

        string name = string.IsNullOrWhiteSpace(record.Title) ? "--" : record.Title;
        var pending = record.Fields.SelectMany(field => field.Values.Select(value => field with { Value = value })).ToList();
        string? username = NonBlank(record.Login);
        string? password = NonBlank(record.Password);
        var uris = new List<string>();
        if (NonBlank(record.LoginUrl) is { } loginUrl)
        {
            uris.Add(loginUrl);
        }

        string recordType = record.Type ?? "";
        VaultItemType type = ChooseType(recordType, username, password, uris, pending);
        var fields = new List<CustomField>();
        var notes = new List<string>();
        CardData? card = null;
        IdentityData? identity = null;
        SshKeyData? sshKey = null;
        switch (type)
        {
            case VaultItemType.Card:
                card = MapCard(pending, fields);
                AddLoginAsFields(fields, username, password, "Password", uris);
                break;
            case VaultItemType.Identity:
                identity = MapIdentity(recordType, pending);
                AddLoginAsFields(fields, username, password, "Password", uris);
                break;
            case VaultItemType.SshKey:
                sshKey = MapSshKey(pending);
                AddLoginAsFields(fields, username, password, "Password", uris);
                if (Take(pending, "host", null) is { } host)
                {
                    AddHost(fields, host.Value);
                }

                break;
            case VaultItemType.SecureNote:
                AddLoginAsFields(fields, username, password, recordType == "sshKeys" ? "Passphrase" : "Password", uris);
                break;
            case VaultItemType.Login:
            default:
                break;
        }

        string? totp = null;
        var passkeys = new List<Fido2Credential>();
        foreach (KeeperField field in pending)
        {
            MapField(field, type, name, fields, uris, passkeys, ref totp, notes, warnings);
        }

        MapFolders(record, fields, notes);
        if (record.ReferenceCount > 0)
        {
            notes.Add(string.Create(CultureInfo.InvariantCulture,
                $"Record references {record.ReferenceCount} other Keeper record(s); references are not carried over."));
        }

        if (record.HasSchema)
        {
            notes.Add("Keeper record schema (field order and empty fields) is not carried over.");
        }

        return new VaultItem
        {
            Type = type,
            Name = name,
            FolderPath = record.Folders.Select(KeeperFolderPath.Join).FirstOrDefault(path => path is not null),
            Notes = NonBlank(record.Notes),
            Fields = fields,
            Login = type == VaultItemType.Login
                ? new LoginData
                {
                    Username = username,
                    Password = password,
                    Totp = totp,
                    Uris = [.. uris.Select(uri => new LoginUri(uri))],
                    Fido2Credentials = passkeys,
                }
                : null,
            Card = card,
            Identity = identity,
            SshKey = sshKey,
            Origin = new ItemOrigin(Provider, sourceFile, record.Uid,
                record.LastModified is { } seconds ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null),
            Attachments = [.. record.Attachments.Select(attachment => MapAttachment(attachment, name, zipFiles, warnings))],
            ConversionNotes = notes,
        };
    }

    private static VaultItemType ChooseType(string recordType, string? username, string? password, List<string> uris, List<KeeperField> pending)
    {
        if (recordType == "bankCard")
        {
            return VaultItemType.Card;
        }

        if (IdentityTypes.Contains(recordType))
        {
            return VaultItemType.Identity;
        }

        if (recordType == "sshKeys")
        {
            KeeperField? keyPair = pending.Find(field => field.Type == "keyPair");
            return keyPair is not null && ObjectText(keyPair.Value, "privateKey") is not null ? VaultItemType.SshKey : VaultItemType.SecureNote;
        }

        bool hasLoginData = username is not null || password is not null || uris.Count > 0
            || pending.Exists(field => (field.Type == "url" && ScalarText(field.Value) is not null) || field.Type == "passkey");
        return hasLoginData ? VaultItemType.Login : VaultItemType.SecureNote;
    }

    private static CardData MapCard(List<KeeperField> pending, List<CustomField> fields)
    {
        var card = new CardData { CardholderName = ScalarText(Take(pending, "text", "cardholderName")?.Value) };
        if (Take(pending, "paymentCard", null) is { } payment)
        {
            card = card with
            {
                Number = ObjectText(payment.Value, "cardNumber"),
                Code = ObjectText(payment.Value, "cardSecurityCode"),
            };
            if (ObjectText(payment.Value, "cardExpirationDate") is { } expiry)
            {
                string[] parts = expiry.Split('/');
                if (parts.Length == 2)
                {
                    card = card with { ExpMonth = parts[0], ExpYear = parts[1] };
                }
                else
                {
                    fields.Add(new CustomField("Expiration date", expiry, CustomFieldType.Text));
                }
            }
        }

        if (ScalarText(Take(pending, "pinCode", null)?.Value) is { } pin)
        {
            fields.Add(new CustomField("PIN", pin, CustomFieldType.Hidden));
        }

        return card;
    }

    private static IdentityData MapIdentity(string recordType, List<KeeperField> pending)
    {
        var identity = new IdentityData();
        if (Take(pending, "name", "") is { } name)
        {
            identity = identity with
            {
                FirstName = ObjectText(name.Value, "first"),
                MiddleName = ObjectText(name.Value, "middle"),
                LastName = ObjectText(name.Value, "last"),
            };
        }

        if (Take(pending, "address", null) is { } address)
        {
            identity = identity with
            {
                Address1 = ObjectText(address.Value, "street1"),
                Address2 = ObjectText(address.Value, "street2"),
                City = ObjectText(address.Value, "city"),
                State = ObjectText(address.Value, "state"),
                PostalCode = ObjectText(address.Value, "zip"),
                Country = ObjectText(address.Value, "country"),
            };
        }

        string? NumberOf(string label) => ScalarText(Take(pending, "accountNumber", label)?.Value);

        return identity with
        {
            Phone = Take(pending, "phone", null) is { } phone ? NonBlank(FormatPhone(phone.Value)) : null,
            Email = ScalarText(Take(pending, "email", null)?.Value),
            Company = ScalarText(Take(pending, "text", "company")?.Value),
            PassportNumber = recordType == "passport" ? NumberOf("passportNumber") : null,
            LicenseNumber = recordType == "driverLicense" ? NumberOf("dlNumber") : null,
            Ssn = recordType == "ssnCard" ? NumberOf("identityNumber") : null,
        };
    }

    private static SshKeyData MapSshKey(List<KeeperField> pending)
    {
        KeeperField keyPair = Take(pending, "keyPair", null)!;
        return new SshKeyData
        {
            PrivateKey = ObjectText(keyPair.Value, "privateKey"),
            PublicKey = ObjectText(keyPair.Value, "publicKey"),
            KeyFingerprint = null,
        };
    }

    private static void AddLoginAsFields(List<CustomField> fields, string? username, string? password, string passwordName, List<string> uris)
    {
        if (username is not null)
        {
            fields.Add(new CustomField("Username", username, CustomFieldType.Text));
        }

        if (password is not null)
        {
            fields.Add(new CustomField(passwordName, password, CustomFieldType.Hidden));
        }

        fields.AddRange(uris.Select(uri => new CustomField("URL", uri, CustomFieldType.Text)));
        uris.Clear();
    }

    private static void MapField(
        KeeperField field,
        VaultItemType type,
        string itemName,
        List<CustomField> fields,
        List<string> uris,
        List<Fido2Credential> passkeys,
        ref string? totp,
        List<string> notes,
        ICollection<string> warnings)
    {
        JsonElement value = field.Value;
        switch (field.Type)
        {
            case "oneTimeCode":
            case "" when field.Label == "TFC:Keeper":
                if (ScalarText(value) is { } code)
                {
                    if (type == VaultItemType.Login && totp is null)
                    {
                        totp = code;
                    }
                    else
                    {
                        fields.Add(new CustomField("TOTP", code, CustomFieldType.Hidden));
                    }
                }

                return;
            case "url":
                if (ScalarText(value) is { } url)
                {
                    if (type == VaultItemType.Login)
                    {
                        uris.Add(url);
                    }
                    else
                    {
                        fields.Add(new CustomField("URL", url, CustomFieldType.Text));
                    }
                }

                return;
            case "passkey":
                if (type == VaultItemType.Login && KeeperPasskey.TryConvert(value) is { } passkey)
                {
                    passkeys.Add(passkey);
                    return;
                }

                fields.Add(new CustomField(field.DisplayName, Compact(value), CustomFieldType.Hidden));
                if (type == VaultItemType.Login)
                {
                    warnings.Add($"\"{itemName}\": a passkey could not be converted and is kept as a hidden field.");
                }
                else
                {
                    notes.Add("Passkey kept as a hidden field because the item is not a login.");
                }

                return;
            case "host":
                AddHost(fields, value);
                return;
            case "keyPair":
                AddIfPresent(fields, "Public key", ObjectText(value, "publicKey"), CustomFieldType.Text);
                AddIfPresent(fields, "Private key", ObjectText(value, "privateKey"), CustomFieldType.Hidden);
                return;
            case "securityQuestion":
                string? question = ObjectText(value, "question");
                string? answer = ObjectText(value, "answer");
                if (answer is not null)
                {
                    fields.Add(new CustomField(question ?? "Security question answer", answer, CustomFieldType.Hidden));
                }
                else
                {
                    AddIfPresent(fields, "Security question", question, CustomFieldType.Text);
                }

                return;
            default:
                string? text = FormatValue(field.Type, value);
                if (text is null)
                {
                    return;
                }

                CustomFieldType fieldType = field.Type == "checkbox" && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? CustomFieldType.Boolean
                    : HiddenTypes.Contains(field.Type) ? CustomFieldType.Hidden : CustomFieldType.Text;
                fields.Add(new CustomField(field.DisplayName.Length > 0 ? field.DisplayName : "field", text, fieldType));
                return;
        }
    }

    private static void AddHost(List<CustomField> fields, JsonElement host)
    {
        AddIfPresent(fields, "Hostname", ObjectText(host, "hostName"), CustomFieldType.Text);
        AddIfPresent(fields, "Port", ObjectText(host, "port"), CustomFieldType.Text);
    }

    private static void AddIfPresent(List<CustomField> fields, string name, string? value, CustomFieldType type)
    {
        if (value is not null)
        {
            fields.Add(new CustomField(name, value, type));
        }
    }

    private static void MapFolders(KeeperRecord record, List<CustomField> fields, List<string> notes)
    {
        string[] others = [.. record.Folders.Select(KeeperFolderPath.Join).OfType<string>().Skip(1).Distinct(StringComparer.Ordinal)];
        if (others.Length > 0)
        {
            fields.Add(new CustomField("Additional folders", string.Join('\n', others), CustomFieldType.Text));
            notes.Add(string.Create(CultureInfo.InvariantCulture,
                $"Record is also in {others.Length} other folder(s); they are listed in the field \"Additional folders\"."));
        }

        if (record.Folders.Any(folder => folder.CanEdit || folder.CanShare))
        {
            notes.Add("Keeper shared folder permissions (can edit, can share) are not carried over.");
        }
    }

    private static AttachmentRef MapAttachment(KeeperAttachment attachment, string itemName, IReadOnlyDictionary<string, long>? zipFiles, ICollection<string> warnings)
    {
        string path = "files/" + attachment.FileUid;
        if (zipFiles is null)
        {
            return new AttachmentRef(attachment.Name, null, null);
        }

        if (attachment.FileUid is not null && zipFiles.TryGetValue(path, out long size))
        {
            return new AttachmentRef(attachment.Name, size, path);
        }

        warnings.Add($"\"{itemName}\": attachment \"{attachment.Name}\" is listed but not included in the zip.");
        return new AttachmentRef(attachment.Name, null, null);
    }

    private static KeeperField? Take(List<KeeperField> pending, string type, string? label)
    {
        int index = pending.FindIndex(field => field.Type == type && (label is null || string.Equals(field.Label, label, StringComparison.Ordinal)));
        if (index < 0)
        {
            return null;
        }

        KeeperField field = pending[index];
        pending.RemoveAt(index);
        return field;
    }

    private static string? FormatValue(string type, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                string? text = NonBlank(value.GetString());
                return DateTypes.Contains(type) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
                    ? FormatDate(parsed) ?? text
                    : text;
            case JsonValueKind.Number:
                return DateTypes.Contains(type) && value.TryGetInt64(out long milliseconds)
                    ? FormatDate(milliseconds) ?? value.GetRawText()
                    : value.GetRawText();
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Object:
                return NonBlank(type switch
                {
                    "name" => JoinPresent(" ", value, "first", "middle", "last"),
                    "address" => JoinPresent(", ", value, "street1", "street2", "city", "state", "zip", "country"),
                    "phone" => FormatPhone(value),
                    "bankAccount" => FormatBankAccount(value),
                    _ => Compact(value),
                });
            case JsonValueKind.Array:
                return Compact(value);
            default:
                return null;
        }
    }

    private static string? FormatDate(long milliseconds)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string FormatPhone(JsonElement phone)
    {
        var parts = new List<string>();
        if (ObjectText(phone, "region") is { } region)
        {
            parts.Add($"({region})");
        }

        if (ObjectText(phone, "number") is { } number)
        {
            parts.Add(number);
        }

        if (ObjectText(phone, "ext") is { } extension)
        {
            parts.Add($"ext. {extension}");
        }

        if (ObjectText(phone, "type") is { } kind)
        {
            parts.Add($"({kind})");
        }

        return string.Join(' ', parts);
    }

    private static string FormatBankAccount(JsonElement account)
    {
        var parts = new List<string>();
        if ((ObjectText(account, "otherType") ?? ObjectText(account, "accountType")) is { } kind)
        {
            parts.Add($"Type: {kind}");
        }

        if (ObjectText(account, "accountNumber") is { } number)
        {
            parts.Add($"Account Number: {number}");
        }

        if (ObjectText(account, "routingNumber") is { } routing)
        {
            parts.Add($"Routing Number: {routing}");
        }

        return string.Join(", ", parts);
    }

    private static string JoinPresent(string separator, JsonElement value, params string[] names) =>
        string.Join(separator, names.Select(name => ObjectText(value, name)).OfType<string>());

    private static string? ObjectText(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out JsonElement property) ? ScalarText(property) : null;

    private static string? ScalarText(JsonElement? value) => value?.ValueKind switch
    {
        JsonValueKind.String => NonBlank(value.Value.GetString()),
        JsonValueKind.Number => value.Value.GetRawText(),
        _ => null,
    };

    private static string Compact(JsonElement value)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, CompactJson))
        {
            value.WriteTo(writer);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static string? NonBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
