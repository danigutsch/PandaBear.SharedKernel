using System.Security.Cryptography;
using System.Text.Json.Nodes;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Keeper;

internal static class KeeperTestData
{
    public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '='));
    }

    public static JsonObject PasskeyJwk(ECParameters parameters) => new()
    {
        ["kty"] = "EC",
        ["crv"] = "P-256",
        ["x"] = Base64Url(parameters.Q.X!),
        ["y"] = Base64Url(parameters.Q.Y!),
        ["d"] = Base64Url(parameters.D!),
        ["key_ops"] = new JsonArray("sign"),
        ["ext"] = true,
    };

    public static string LoginWithPasskey(JsonObject privateKey, string credentialId) =>
        new JsonObject
        {
            ["records"] = new JsonArray(new JsonObject
            {
                ["uid"] = "rec-passkey",
                ["title"] = "Passkey login",
                ["$type"] = "login",
                ["login"] = "alice",
                ["login_url"] = "https://passkey.example.com",
                ["custom_fields"] = new JsonObject
                {
                    ["$passkey"] = new JsonObject
                    {
                        ["privateKey"] = privateKey,
                        ["credentialId"] = credentialId,
                        ["signCount"] = 7,
                        ["userId"] = "dXNlci1oYW5kbGU",
                        ["relyingParty"] = "passkey.example.com",
                        ["username"] = "alice@example.com",
                        ["createdDate"] = 1751241600000,
                    },
                },
            }),
        }.ToJsonString();

    public static string SshKeyRecord(string privateKeyPem, string publicKey) =>
        new JsonObject
        {
            ["records"] = new JsonArray(new JsonObject
            {
                ["uid"] = "rec-ssh",
                ["title"] = "Deploy key",
                ["$type"] = "sshKeys",
                ["login"] = "deploy",
                ["password"] = "fixture-passphrase",
                ["custom_fields"] = new JsonObject
                {
                    ["$keyPair"] = new JsonObject { ["publicKey"] = publicKey, ["privateKey"] = privateKeyPem },
                    ["$host"] = new JsonObject { ["hostName"] = "git.example.com", ["port"] = "22" },
                },
            }),
        }.ToJsonString();

    public static string CommanderZipExport() =>
        new JsonObject
        {
            ["records"] = new JsonArray(new JsonObject
            {
                ["uid"] = "rec-files",
                ["title"] = "Record with files",
                ["$type"] = "file",
                ["last_modified"] = 1767225600,
                ["attachments"] = new JsonArray(
                    new JsonObject { ["file_uid"] = "file-1", ["name"] = "scan.pdf", ["mime"] = "application/pdf" },
                    new JsonObject { ["file_uid"] = "file-2", ["name"] = "missing.txt" }),
            }),
        }.ToJsonString();

    public static VaultExport ReadCommander() =>
        Tool.Formats.Keeper.KeeperJsonReader.Read(Fixture.Read("keeper/commander.json"), "commander.json").Export;

    public static CustomField Field(string name, string value, CustomFieldType type = CustomFieldType.Text) => new(name, value, type);
}
