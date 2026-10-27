using System.Security.Cryptography;
using System.Text.Json.Nodes;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Formats.Keeper;

/// <summary>Builds Keeper export JSON and helper values for the Keeper reader tests.</summary>
internal static class KeeperTestData
{
    /// <summary>Encodes bytes as base64url text without padding.</summary>
    /// <param name="bytes">The bytes to encode.</param>
    public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Decodes base64url text with or without padding.</summary>
    /// <param name="value">The base64url text.</param>
    public static byte[] FromBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '='));
    }

    /// <summary>Returns the JSON Web Key of a P-256 signing key, with its public coordinates and private scalar base64url encoded.</summary>
    /// <param name="parameters">The EC parameters that include the private scalar.</param>
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

    /// <summary>Returns a Keeper export JSON with one login record, "Passkey login", that holds a passkey for passkey.example.com with sign count 7.</summary>
    /// <param name="privateKey">The JSON Web Key of the passkey.</param>
    /// <param name="credentialId">The base64url credential ID of the passkey.</param>
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

    /// <summary>Returns a Keeper export JSON with one SSH key record, "Deploy key", that holds the given key pair and the host git.example.com on port 22.</summary>
    /// <param name="privateKeyPem">The PEM text of the private key.</param>
    /// <param name="publicKey">The public key text.</param>
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

    /// <summary>Returns a Keeper export JSON with the record "Record with files" that lists the attachments scan.pdf (file-1) and missing.txt (file-2).</summary>
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

    /// <summary>Returns the vault that the Keeper JSON reader produces from the keeper/commander.json fixture.</summary>
    public static VaultExport ReadCommander() =>
        Tool.Formats.Keeper.KeeperJsonReader.Read(Fixture.Read("keeper/commander.json"), "commander.json").Export;

    /// <summary>Creates a custom field for comparison with the fields that a reader produced.</summary>
    /// <param name="name">The field name.</param>
    /// <param name="value">The field value.</param>
    /// <param name="type">The field type, text by default.</param>
    public static CustomField Field(string name, string value, CustomFieldType type = CustomFieldType.Text) => new(name, value, type);
}
