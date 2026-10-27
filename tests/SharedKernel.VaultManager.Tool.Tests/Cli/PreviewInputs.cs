using System.Text.Json.Nodes;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Pipeline;
using static PandaBear.VaultManager.Tool.Tests.Pipeline.PipelineInputs;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>
/// A vault and its catalog with shared notes for the apply preview tests. The login holds a password, a TOTP, notes, a
/// text field, and a masked address whose credential parts must never be printed.
/// </summary>
internal static class PreviewInputs
{
    /// <summary>The login password that previews must never print.</summary>
    public const string Password = "fixture-preview-password";

    /// <summary>The shared notes text of the login that previews must never print.</summary>
    public const string NotesText = "fixture preview notes text";

    /// <summary>The value of the login's "Plan" text field that previews must never print.</summary>
    public const string FieldValue = "fixture-preview-field-value";

    /// <summary>The password part of the masked address's user info, which must never be printed.</summary>
    public const string UriSecret = "fixture-preview-uri-secret";

    /// <summary>The token query value of the masked address, which must never be printed.</summary>
    public const string UriToken = "fixture-preview-uri-token";

    /// <summary>The login address that carries both <see cref="UriSecret" /> and <see cref="UriToken" />.</summary>
    public const string MaskedUri = "https://alice:" + UriSecret + "@example.com/reset?token=" + UriToken;

    /// <summary>The TOTP secret of the login that previews must never print.</summary>
    public static readonly string TotpSecret = PipelineSecrets.TotpSecret(2200);

    /// <summary>Writes the vault, makes its catalog, shares the notes in the decisions file, and makes the catalog again.</summary>
    /// <returns>The vault, catalog, and decisions files.</returns>
    public static async Task<(string Vault, string Catalog, string Decisions)> SharedCatalog(string directory, VaultCommandEnvironment environment, CancellationToken ct)
    {
        JsonObject login = BitwardenLogin("bw-github", "GitHub", "alice", Password, "https://github.com/login");
        login["notes"] = NotesText;
        login["folderId"] = "f1";
        login["fields"] = new JsonArray(new JsonObject { ["name"] = "Plan", ["value"] = FieldValue, ["type"] = 0 });
        JsonObject data = login["login"].ShouldNotBeNull().AsObject();
        data["totp"] = PipelineSecrets.TotpUri(TotpSecret);
        data["uris"].ShouldNotBeNull().AsArray().Add(new JsonObject { ["uri"] = MaskedUri });
        var document = JsonNode.Parse(BitwardenJson(login)).ShouldNotBeNull();
        document["folders"] = new JsonArray(new JsonObject { ["id"] = "f1", ["name"] = "Work" });
        string vault = Write(directory, "vault.json", document.ToJsonString());
        string catalog = Path.Combine(directory, "catalog.json");
        string decisions = Path.Combine(directory, "decisions.json");

        (await CommandLineRun.Invoke(environment, ["catalog", vault, "-o", catalog, "--decisions", decisions], ct)).ExitCode.ShouldBe(0);
        var decisionsDocument = JsonNode.Parse(await File.ReadAllTextAsync(decisions, ct)).ShouldNotBeNull();
        decisionsDocument["notes"].ShouldNotBeNull()[0].ShouldNotBeNull()["decision"] = "Share";
        await File.WriteAllTextAsync(decisions, decisionsDocument.ToJsonString(), ct);
        (await CommandLineRun.Invoke(environment, ["catalog", vault, "-o", catalog, "--decisions", decisions, "--force"], ct)).ExitCode.ShouldBe(0);
        return (vault, catalog, decisions);
    }

    /// <summary>Edits the single item of a catalog file.</summary>
    public static async Task EditItem(string catalog, Action<JsonNode> edit, CancellationToken ct)
    {
        var document = JsonNode.Parse(await File.ReadAllTextAsync(catalog, ct)).ShouldNotBeNull();
        edit(document["items"].ShouldNotBeNull()[0].ShouldNotBeNull());
        await File.WriteAllTextAsync(catalog, document.ToJsonString(), ct);
    }
}
