using System.Text.Json.Nodes;
using PandaBear.VaultManager.Tool.Crypto;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Tests.Pipeline;
using static PandaBear.VaultManager.Tool.Tests.Pipeline.PipelineInputs;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Writes synthetic exports for command-line tests. Secret-shaped values are built at test runtime.</summary>
internal static class CliInputs
{
    public const string KeeperPassword = "fixture-keeper-password";
    public const string BitwardenPassword = "fixture-bitwarden-password";
    public const string NotesPassword = "Gh-notes-Passw0rd!";
    public const string KeeperKey = "keeper|keeper.json|rec-1";
    public const string BitwardenKey = "bitwarden|bitwarden.json|bw-1";
    public const string GitHubKey = "keeper|keeper.json|rec-github";

    private static readonly KdfConfig FastPbkdf2 = new(KdfType.Pbkdf2Sha256, 5_000);

    /// <summary>The same login in both inputs with different passwords and no modification times, so the password needs a decision.</summary>
    public static string[] ConflictingLogins(string directory) =>
    [
        Write(directory, "keeper.json", KeeperJson(KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com", modified: false))),
        Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", BitwardenPassword, "https://example.com", revised: false))),
    ];

    /// <summary>The same login in both inputs. The Bitwarden one is newer, so the merge needs no decision.</summary>
    public static string[] NewerBitwardenLogin(string directory) =>
    [
        Write(directory, "keeper.json", KeeperJson(KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com"))),
        Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", BitwardenPassword, "https://example.com"))),
    ];

    /// <summary>A Keeper login whose notes hold pre-accepted recovery codes and a password, a finding that needs accepting.</summary>
    public static string CodesAndPasswordInNotes(string directory, string[] codes) =>
        Write(directory, "keeper.json", KeeperJson(KeeperLogin("rec-github", "GitHub", "alice", url: "https://github.com", notes: PipelineSecrets.RecoveryNotes(codes, NotesPassword))));

    /// <summary>
    /// A Keeper login whose notes hold only pre-accepted recovery codes, and a newer Bitwarden login for the same account.
    /// </summary>
    public static string[] CodesInKeeperNotes(string directory, string[] codes) =>
    [
        Write(directory, "keeper.json", KeeperJson(KeeperLogin("rec-github", "GitHub", "alice", url: "https://github.com", notes: "GitHub recovery codes\n" + string.Join('\n', codes)))),
        Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-github", "GitHub", "alice", BitwardenPassword, "https://github.com"))),
    ];

    /// <summary>A Bitwarden export protected with a file password, with fast key derivation.</summary>
    public static string ProtectedBitwarden(string directory, string filePassword) =>
        Write(directory, "protected.json", PasswordProtectedExport.Encrypt(
            BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", BitwardenPassword, "https://example.com")),
            filePassword,
            FastPbkdf2));

    /// <summary>
    /// A Keeper Commander zip with a login in a folder and a file record that lists one file included in the zip and one
    /// missing from it.
    /// </summary>
    public static string KeeperZipWithFolderAndAttachments(string directory)
    {
        JsonObject login = KeeperLogin("rec-github", "GitHub", "alice", KeeperPassword, "https://github.com");
        login["folders"] = new JsonArray(new JsonObject { ["folder"] = "Personal" });
        var files = new JsonObject
        {
            ["uid"] = "rec-files",
            ["title"] = "Backup",
            ["$type"] = "file",
            ["last_modified"] = KeeperModified.ToUnixTimeSeconds(),
            ["attachments"] = new JsonArray(
                new JsonObject { ["file_uid"] = "file-codes", ["name"] = "codes.txt" },
                new JsonObject { ["file_uid"] = "file-photo", ["name"] = "photo.png" }),
        };
        return Write(directory, "keeper.zip", CommanderZip(KeeperJson(login, files), ("files/file-codes", "fixture attachment text\n")));
    }

    /// <summary>A Bitwarden export with a login in a folder and a note without a modification time.</summary>
    public static string BitwardenWithFolder(string directory, string folderName = "Work")
    {
        JsonObject mail = BitwardenLogin("bw-mail", "Mail", "bob", BitwardenPassword, "https://mail.example.com");
        mail["folderId"] = "folder-work";
        JsonObject note = BitwardenNote("bw-wifi", "Wifi", "fixture wifi note");
        note.Remove("revisionDate");
        var export = new JsonObject
        {
            ["encrypted"] = false,
            ["folders"] = new JsonArray(new JsonObject { ["id"] = "folder-work", ["name"] = folderName }),
            ["items"] = new JsonArray(mail, note),
        };
        return Write(directory, "bitwarden.json", export.ToJsonString());
    }
}
