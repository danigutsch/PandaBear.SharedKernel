using System.Globalization;
using System.Text.Json.Nodes;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

/// <summary>Builds synthetic Keeper Commander and Bitwarden exports for pipeline tests and writes them to disk.</summary>
internal static class PipelineInputs
{
    public static readonly DateTimeOffset KeeperModified = new(2024, 3, 1, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset BitwardenRevision = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    public static string Write(string directory, string name, string text)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, text);
        return path;
    }

    public static string Write(string directory, string name, byte[] bytes)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public static string KeeperJson(params JsonObject[] records) =>
        new JsonObject { ["records"] = new JsonArray([.. records]) }.ToJsonString();

    public static byte[] CommanderZip(string exportJson, params (string Path, string Text)[] files) =>
        TestZip.Create([("export.json", TestZip.Utf8(exportJson)), .. files.Select(file => (file.Path, TestZip.Utf8(file.Text)))]);

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

    public static JsonObject KeeperNote(string uid, string title, string notes) => new()
    {
        ["uid"] = uid,
        ["title"] = title,
        ["$type"] = "encryptedNotes",
        ["notes"] = notes,
        ["last_modified"] = KeeperModified.ToUnixTimeSeconds(),
    };

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

    public static JsonObject KeeperFileRecord(string uid, string title, string fileUid, string fileName) =>
        KeeperFileRecord(uid, title, (fileUid, fileName));

    public static JsonObject KeeperFileRecord(string uid, string title, params (string FileUid, string FileName)[] files) => new()
    {
        ["uid"] = uid,
        ["title"] = title,
        ["$type"] = "file",
        ["last_modified"] = KeeperModified.ToUnixTimeSeconds(),
        ["attachments"] = Attachments(files),
    };

    public static JsonObject WithAttachments(JsonObject record, params (string FileUid, string FileName)[] files)
    {
        record["attachments"] = Attachments(files);
        return record;
    }

    public static string BitwardenJson(params JsonObject[] items) =>
        new JsonObject { ["encrypted"] = false, ["folders"] = new JsonArray(), ["items"] = new JsonArray([.. items]) }.ToJsonString();

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
