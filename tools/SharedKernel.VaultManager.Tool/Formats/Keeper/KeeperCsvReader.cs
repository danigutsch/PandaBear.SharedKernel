using System.Globalization;
using System.Text.Json;
using PandaBear.VaultManager.Tool.Formats.Csv;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Formats.Keeper;

/// <summary>
/// Reads Keeper CSV exports. Columns are <c>Folder,Title,Login,Password,Website Address,Notes,Shared Folder</c>, then
/// name/value pairs. Commander rows start the pairs with <c>$record_uid</c> and <c>$type</c> and use typed keys; web vault
/// rows use English labels and join combined values with <c>" | "</c>.
/// </summary>
internal static class KeeperCsvReader
{
    private const int FirstPairColumn = 7;

    private static readonly HashSet<string> HiddenLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Security Code", "PIN", "PIN Code", "Card Number", "Account Number", "Routing Number", "Password", "Secret",
        "Private Key", "Passphrase", "License Number", "Social Security Number",
    };

    private static readonly HashSet<string> DateLabels = new(StringComparer.Ordinal)
    {
        "Date of Birth", "Date", "Date Issued", "Expiration Date",
    };

    /// <summary>Reads a Keeper CSV export.</summary>
    /// <param name="text">File contents.</param>
    /// <param name="sourceFile">File name recorded as the item origin.</param>
    /// <returns>Items and warnings.</returns>
    /// <exception cref="InvalidDataException">The file is not well-formed CSV.</exception>
    public static VaultReadResult Read(string text, string sourceFile)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(sourceFile);

        var warnings = new List<string>
        {
            "Keeper CSV exports lose record types, field structure, timestamps, and attachments; export JSON from Keeper for a complete conversion.",
        };
        var items = new List<VaultItem>();
        foreach (CsvRecord row in CsvReader.Parse(text))
        {
            if (row.Fields.Count == 1 && row.Fields[0].Length == 0)
            {
                continue;
            }

            if (row.Fields.Count < 6)
            {
                warnings.Add(string.Create(CultureInfo.InvariantCulture,
                    $"Keeper CSV line {row.LineNumber} has fewer than 6 columns and was skipped."));
                continue;
            }

            items.Add(KeeperRecordMapper.Map(ParseRow(row.Fields), sourceFile, zipFiles: null, warnings));
        }

        return new VaultReadResult(new VaultExport(items), warnings);
    }

    private static KeeperRecord ParseRow(IReadOnlyList<string> row)
    {
        string? uid = null;
        string? type = null;
        var fields = new List<KeeperField>();
        for (int index = FirstPairColumn; index + 1 < row.Count; index += 2)
        {
            string key = row[index].Trim();
            string value = row[index + 1];
            if (key.Length == 0 || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            switch (key)
            {
                case "$record_uid":
                    uid = value.Trim();
                    break;
                case "$type":
                    type = value.Trim();
                    break;
                default:
                    fields.Add(key.StartsWith('$') ? TypedField(key, value) : LabeledField(key, value, ref type));
                    break;
            }
        }

        string sharedFolder = Cell(row, 6).Trim();
        bool canEdit = false;
        bool canShare = false;
        for (bool stripped = true; stripped;)
        {
            stripped = false;
            if (sharedFolder.EndsWith("#edit", StringComparison.Ordinal))
            {
                (sharedFolder, canEdit, stripped) = (sharedFolder[..^5], true, true);
            }

            if (sharedFolder.EndsWith("#reshare", StringComparison.Ordinal))
            {
                (sharedFolder, canShare, stripped) = (sharedFolder[..^8], true, true);
            }
        }

        string folder = row[0].Trim();
        return new KeeperRecord
        {
            Uid = uid,
            Type = type,
            Title = row[1].Trim(),
            Login = row[2],
            Password = row[3],
            LoginUrl = row[4].Trim(),
            Notes = row[5].TrimEnd(),
            Fields = fields,
            Folders = folder.Length > 0 || sharedFolder.Length > 0
                ? [new KeeperFolder(sharedFolder.Length > 0 ? sharedFolder : null, folder.Length > 0 ? folder : null, canEdit, canShare)]
                : [],
        };
    }

    private static KeeperField TypedField(string key, string value)
    {
        (string type, string label) = KeeperFieldKey.Parse(key);
        return new KeeperField(type, label, TypedValue(type, value));
    }

    private static JsonElement TypedValue(string type, string value)
    {
        string trimmed = value.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}') && TryParseObject(trimmed) is { } parsed)
        {
            return parsed;
        }

        string[] lines = value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        switch (type)
        {
            case "oneTimeCode":
            case "url":
                return Element(writer => WriteArray(writer, lines));
            case "checkbox":
                return Element(writer => writer.WriteBooleanValue(trimmed is "1" or "t" or "true" or "True" or "TRUE"));
            case "date":
            case "birthDate":
            case "expirationDate":
                return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out long milliseconds)
                    ? Element(writer => writer.WriteNumberValue(milliseconds))
                    : String(value);
            case "paymentCard":
                return PaymentCard(trimmed);
            case "name":
                return Name(trimmed);
            case "keyPair":
            case "privateKey":
                return Object(("privateKey", value));
            case "host":
                int colon = trimmed.LastIndexOf(':');
                return colon > 0 && trimmed[(colon + 1)..].All(char.IsAsciiDigit) && colon + 1 < trimmed.Length
                    ? Object(("hostName", trimmed[..colon]), ("port", trimmed[(colon + 1)..]))
                    : Object(("hostName", trimmed));
            case "securityQuestion":
                return Element(writer =>
                {
                    writer.WriteStartArray();
                    foreach (string line in lines)
                    {
                        int mark = line.IndexOf("? ", StringComparison.Ordinal);
                        WriteObject(writer, mark < 0
                            ? [("question", line)]
                            : [("question", line[..(mark + 1)]), ("answer", line[(mark + 2)..])]);
                    }

                    writer.WriteEndArray();
                });
            default:
                return String(value);
        }
    }

    private static KeeperField LabeledField(string label, string value, ref string? type)
    {
        switch (label)
        {
            case "TFC:Keeper":
                return new KeeperField("oneTimeCode", "", String(value.Trim()));
            case "Name":
                return new KeeperField("name", "", Name(value.Trim()));
            case "Passport Number":
                type ??= "passport";
                return new KeeperField("accountNumber", "passportNumber", String(value));
            case "Driver's License Number":
                type ??= "driverLicense";
                return new KeeperField("accountNumber", "dlNumber", String(value));
            case "Bank Account":
                type ??= "bankAccount";
                string[] parts = value.Split(" | ");
                return new KeeperField("bankAccount", label, Object(
                    ("accountType", parts.ElementAtOrDefault(0)),
                    ("accountNumber", parts.ElementAtOrDefault(1)),
                    ("routingNumber", parts.ElementAtOrDefault(2))));
            default:
                if (DateLabels.Contains(label) && DateTime.TryParseExact(value.Trim(), "M/d/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                {
                    return new KeeperField("", label, String(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                }

                return new KeeperField(HiddenLabels.Contains(label) ? "secret" : "", label, String(value));
        }
    }

    private static JsonElement PaymentCard(string value)
    {
        string? expiry = null;
        var others = new List<string>();
        foreach (string token in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (expiry is null && token.Contains('/', StringComparison.Ordinal))
            {
                expiry = token;
            }
            else
            {
                others.Add(token);
            }
        }

        return others.Count <= 2
            ? Object(("cardNumber", others.ElementAtOrDefault(0)), ("cardExpirationDate", expiry), ("cardSecurityCode", others.ElementAtOrDefault(1)))
            : String(value);
    }

    // Commander writes "Last, First Middle"; the web vault writes "First Middle Last".
    private static JsonElement Name(string value)
    {
        int comma = value.IndexOf(',', StringComparison.Ordinal);
        string last;
        string[] given;
        if (comma >= 0)
        {
            last = value[..comma].Trim();
            given = value[(comma + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }
        else
        {
            string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            last = parts.Length > 1 ? parts[^1] : "";
            given = parts.Length > 1 ? parts[..^1] : parts;
        }

        return Object(("first", given.ElementAtOrDefault(0)), ("middle", given.Length > 1 ? string.Join(' ', given[1..]) : null), ("last", last));
    }

    private static JsonElement? TryParseObject(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement String(string value) => Element(writer => writer.WriteStringValue(value));

    private static JsonElement Object(params (string Name, string? Value)[] properties) => Element(writer => WriteObject(writer, properties));

    private static void WriteObject(Utf8JsonWriter writer, (string Name, string? Value)[] properties)
    {
        writer.WriteStartObject();
        foreach ((string name, string? value) in properties)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                writer.WriteString(name, value);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteArray(Utf8JsonWriter writer, string[] values)
    {
        writer.WriteStartArray();
        foreach (string value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static JsonElement Element(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        using var document = JsonDocument.Parse(buffer.ToArray());
        return document.RootElement.Clone();
    }

    private static string Cell(IReadOnlyList<string> row, int index) => index < row.Count ? row[index] : "";
}
