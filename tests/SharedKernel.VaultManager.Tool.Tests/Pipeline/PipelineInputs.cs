using System.Globalization;
using System.Text.Json.Nodes;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

/// <summary>Builds synthetic Keeper Commander and Bitwarden exports for pipeline tests and writes them to disk.</summary>
internal static class PipelineInputs
{
    /// <summary>The last-modified time set on Keeper records, 2024-03-01 12:00 UTC.</summary>
    public static readonly DateTimeOffset KeeperModified = new(2024, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The revision date set on Bitwarden items, 2026-03-01 12:00 UTC.</summary>
    public static readonly DateTimeOffset BitwardenRevision = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The fixed clock time the test environment reports, 2026-10-22 12:00 UTC.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Writes the text to a file of the given name in the directory and returns the file's path.</summary>
    public static string Write(string directory, string name, string text)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>Writes the bytes to a file of the given name in the directory and returns the file's path.</summary>
    public static string Write(string directory, string name, byte[] bytes)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>Serializes the records as the <c>records</c> array of a Keeper export JSON document.</summary>
    public static string KeeperJson(params JsonObject[] records) =>
        new JsonObject { ["records"] = new JsonArray([.. records]) }.ToJsonString();

    /// <summary>Builds a Keeper Commander zip holding <c>export.json</c> with the given text plus the extra files.</summary>
    public static byte[] CommanderZip(string exportJson, params (string Path, string Text)[] files) =>
        TestZip.Create([("export.json", TestZip.Utf8(exportJson)), .. files.Select(file => (file.Path, TestZip.Utf8(file.Text)))]);

    /// <summary>Builds a Bitwarden zip holding <c>data.json</c> with the given text plus the extra files.</summary>
    public static byte[] BitwardenZip(string dataJson, params (string Path, string Text)[] files) =>
        TestZip.Create([("data.json", TestZip.Utf8(dataJson)), .. files.Select(file => (file.Path, TestZip.Utf8(file.Text)))]);

    /// <summary>
    /// Bitwarden zip with two logins named <c>Bank</c>, whose attachment folders are <c>Bank</c> and <c>Bank_1</c>, as
    /// Bitwarden names them. Each folder could belong to either login.
    /// </summary>
    public static byte[] TwoBanksZip() => BitwardenZip(
        BitwardenJson(
            BitwardenLogin("bw-bank-one", "Bank", "alice", "fixture-bank-one-password", "https://one.example.com"),
            BitwardenLogin("bw-bank-two", "Bank", "bob", "fixture-bank-two-password", "https://two.example.com")),
        ("attachments/Bank/statement.txt", "fixture statement\n"),
        ("attachments/Bank_1/contract.txt", "fixture contract\n"));

    /// <summary>Creates a Keeper login record, setting only the given optional fields and a last-modified time when <paramref name="modified"/> is true.</summary>
    public static JsonObject KeeperLogin(
        string uid,
        string title,
        string? login = null,
        string? password = null,
        string? url = null,
        string? notes = null,
        string? totp = null,
        bool modified = true)
    {
        var record = new JsonObject { ["uid"] = uid, ["title"] = title, ["$type"] = "login" };
        SetIfPresent(record, "login", login);
        SetIfPresent(record, "password", password);
        SetIfPresent(record, "login_url", url);
        SetIfPresent(record, "notes", notes);
        if (totp is not null)
        {
            record["custom_fields"] = new JsonObject { ["$oneTimeCode"] = totp };
        }

        if (modified)
        {
            record["last_modified"] = KeeperModified.ToUnixTimeSeconds();
        }

        return record;
    }

    /// <summary>Creates a Keeper <c>encryptedNotes</c> record with the given notes and the Keeper last-modified time.</summary>
    public static JsonObject KeeperNote(string uid, string title, string notes) => new()
    {
        ["uid"] = uid,
        ["title"] = title,
        ["$type"] = "encryptedNotes",
        ["notes"] = notes,
        ["last_modified"] = KeeperModified.ToUnixTimeSeconds(),
    };

    /// <summary>Creates a Keeper <c>sshKeys</c> record whose <c>$keyPair</c> custom field holds the given public and private keys.</summary>
    public static JsonObject KeeperSshKey(string uid, string title, string privateKey, string publicKey) => new()
    {
        ["uid"] = uid,
        ["title"] = title,
        ["$type"] = "sshKeys",
        ["last_modified"] = KeeperModified.ToUnixTimeSeconds(),
        ["custom_fields"] = new JsonObject
        {
            ["$keyPair"] = new JsonObject { ["publicKey"] = publicKey, ["privateKey"] = privateKey },
        },
    };

    /// <summary>Creates a Keeper <c>file</c> record with a single attachment of the given file UID and name.</summary>
    public static JsonObject KeeperFileRecord(string uid, string title, string fileUid, string fileName) =>
        KeeperFileRecord(uid, title, (fileUid, fileName));

    /// <summary>Creates a Keeper <c>file</c> record with one attachment for each given file UID and name pair.</summary>
    public static JsonObject KeeperFileRecord(string uid, string title, params (string FileUid, string FileName)[] files) => new()
    {
        ["uid"] = uid,
        ["title"] = title,
        ["$type"] = "file",
        ["last_modified"] = KeeperModified.ToUnixTimeSeconds(),
        ["attachments"] = Attachments(files),
    };

    /// <summary>Sets the record's <c>attachments</c> to the given file UID and name pairs, replacing any existing ones, and returns the same record.</summary>
    public static JsonObject WithAttachments(JsonObject record, params (string FileUid, string FileName)[] files)
    {
        record["attachments"] = Attachments(files);
        return record;
    }

    /// <summary>Serializes the items as an unencrypted Bitwarden export with no folders.</summary>
    public static string BitwardenJson(params JsonObject[] items) =>
        new JsonObject { ["encrypted"] = false, ["folders"] = new JsonArray(), ["items"] = new JsonArray([.. items]) }.ToJsonString();

    /// <summary>Creates a Bitwarden login item with an optional username, password, and single address, and a revision date when <paramref name="revised"/> is true.</summary>
    public static JsonObject BitwardenLogin(string id, string name, string? username, string? password, string? uri, bool revised = true)
    {
        var login = new JsonObject();
        SetIfPresent(login, "username", username);
        SetIfPresent(login, "password", password);
        if (uri is not null)
        {
            login["uris"] = new JsonArray(new JsonObject { ["uri"] = uri });
        }

        var item = new JsonObject { ["id"] = id, ["type"] = 1, ["name"] = name, ["login"] = login };
        if (revised)
        {
            item["revisionDate"] = BitwardenRevision.ToString("O", CultureInfo.InvariantCulture);
        }

        return item;
    }

    /// <summary>Creates a Bitwarden secure-note item with the given notes and the Bitwarden revision date.</summary>
    public static JsonObject BitwardenNote(string id, string name, string notes) => new()
    {
        ["id"] = id,
        ["type"] = 2,
        ["name"] = name,
        ["notes"] = notes,
        ["secureNote"] = new JsonObject { ["type"] = 0 },
        ["revisionDate"] = BitwardenRevision.ToString("O", CultureInfo.InvariantCulture),
    };

    private static JsonArray Attachments((string FileUid, string FileName)[] files) =>
        new([.. files.Select(file => new JsonObject { ["file_uid"] = file.FileUid, ["name"] = file.FileName })]);

    private static void SetIfPresent(JsonObject target, string name, string? value)
    {
        if (value is not null)
        {
            target[name] = value;
        }
    }
}
