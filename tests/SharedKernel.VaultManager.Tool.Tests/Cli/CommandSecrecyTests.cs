using System.Security.Cryptography;
using System.Text.Json.Nodes;
using PandaBear.VaultManager.Tool.Brief;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Tests.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Secrets;
using static PandaBear.VaultManager.Tool.Tests.Pipeline.PipelineInputs;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

public sealed class CommandSecrecyTests : IDisposable
{
    private const string OldPassword = "fixture-old-keeper-password";
    private const string NewPassword = "fixture-new-bitwarden-password";
    private const string NotesPassword = "Gh-notes-Passw0rd!";
    private const string KeeperOnlyPassword = "fixture-keeper-only-password";
    private const string UndatedKeeperPassword = "fixture-undated-keeper-password";
    private const string UndatedBitwardenPassword = "fixture-undated-bitwarden-password";
    private const string ExportPassword = "fixture export password";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-cli-secrecy-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public async Task Command_output_reports_and_decisions_never_contain_vault_values()
    {
        string totpSecret = PipelineSecrets.TotpSecret(1100);
        string totp = PipelineSecrets.TotpUri(totpSecret);
        string[] notesCodes = PipelineSecrets.GitHubRecoveryCodes(1110);
        string[] fileCodes = PipelineSecrets.GitHubRecoveryCodes(1130);
        string privateKey = PemFixtures.Ed25519OpenSsh();
        string zip = Write(directory, "keeper.zip", CommanderZip(
            KeeperJson(
                KeeperLogin("rec-github", "GitHub", "alice", OldPassword, "https://github.com/login", PipelineSecrets.RecoveryNotes(notesCodes, NotesPassword), totp),
                KeeperLogin("rec-keeper", "Keeper only", "carol", KeeperOnlyPassword, "https://keeper-only.example.com"),
                KeeperLogin("rec-undated", "Undated", "dave", UndatedKeeperPassword, "https://undated.example.com", modified: false),
                KeeperSshKey("rec-ssh", "Deploy key", privateKey, PemFixtures.Ed25519PublicKey),
                KeeperFileRecord("rec-files", "GitHub backup", "file-codes", "github-recovery-codes.txt")),
            ("files/file-codes", string.Join('\n', fileCodes))));
        string bitwarden = Write(directory, "bitwarden.json", BitwardenJson(
            BitwardenLogin("bw-github", "GitHub", "alice", NewPassword, "https://github.com"),
            BitwardenLogin("bw-undated", "Undated", "dave", UndatedBitwardenPassword, "https://undated.example.com", revised: false)));
        string output = Path.Combine(directory, "merged.json");
        string decisionsPath = Path.Combine(directory, "decisions.json");
        string scanReport = Path.Combine(directory, "scan.md");
        string mergeReport = Path.Combine(directory, "merge.md");
        string[] mergeArgs = ["merge", zip, bitwarden, "--output", output, "--decisions", decisionsPath, "--report", mergeReport];
        var ct = TestContext.Current.CancellationToken;

        var inspect = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["inspect", zip, bitwarden], ct);
        var scan = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), ["scan", zip, bitwarden, "--report", scanReport], ct);
        var pendingMerge = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), mergeArgs, ct);
        string pendingDecisions = await File.ReadAllTextAsync(decisionsPath, TestContext.Current.CancellationToken);
        string pendingReport = await File.ReadAllTextAsync(mergeReport, TestContext.Current.CancellationToken);
        DecisionsFile.Write(decisionsPath, PendingAnswers.AcceptAll(DecisionsFile.Read(decisionsPath)));
        var merge = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), mergeArgs, ct);

        pendingMerge.ExitCode.ShouldBe(2);
        merge.ExitCode.ShouldBe(0);
        pendingDecisions.ShouldContain("\"Pending\"", StringComparison.Ordinal);
        pendingReport.ShouldContain("## Decisions needed", StringComparison.Ordinal);
        string[] texts =
        [
            inspect.Output, inspect.Error, scan.Output, scan.Error, await File.ReadAllTextAsync(scanReport, TestContext.Current.CancellationToken),
            pendingMerge.Output, pendingMerge.Error, pendingDecisions, pendingReport,
            merge.Output, merge.Error, await File.ReadAllTextAsync(decisionsPath, TestContext.Current.CancellationToken), await File.ReadAllTextAsync(mergeReport, TestContext.Current.CancellationToken),
        ];
        string[] values =
        [
            OldPassword, NewPassword, NotesPassword, KeeperOnlyPassword, UndatedKeeperPassword, UndatedBitwardenPassword, ExportPassword,
            totp, totpSecret, .. notesCodes, .. fileCodes, .. privateKey.Split('\n').Where(line => !line.StartsWith('-')),
        ];
        texts.ShouldAllSatisfy(text => values.ShouldAllSatisfy(value => text.ShouldNotContain(value, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Merge_brief_never_contains_secret_values()
    {
        const string HistoryPassword = "fixture-history-password";
        const string HiddenValue = "fixture-hidden-api-key";
        const string BitwardenNotes = "fixture unshared bitwarden notes";
        const string MaskedUri = "https://alice:fixture-uri-secret@github.com/login?token=fixture-uri-token#fixture-uri-fragment";
        string keeperSecret = PipelineSecrets.TotpSecret(1200);
        string bitwardenSecret = PipelineSecrets.TotpSecret(1210);
        string keeperTotp = PipelineSecrets.TotpUri(keeperSecret);
        string bitwardenTotp = PipelineSecrets.TotpUri(bitwardenSecret);
        string[] notesCodes = PipelineSecrets.GitHubRecoveryCodes(1220);
        string[] fileCodes = PipelineSecrets.GitHubRecoveryCodes(1240);
        string keeperNotes = PipelineSecrets.RecoveryNotes(notesCodes, NotesPassword);
        string keeperKey = PemFixtures.Ed25519OpenSsh();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string bitwardenKey = PemFixtures.Pkcs8(ecdsa);
        string keeperCard = TestSecrets.CardNumber("4", 16);
        string bitwardenCard = TestSecrets.CardNumber("51", 16);
        var keeperCardRecord = new JsonObject
        {
            ["uid"] = "rec-card",
            ["title"] = "Fixture card",
            ["$type"] = "bankCard",
            ["custom_fields"] = new JsonObject
            {
                ["$paymentCard"] = new JsonObject { ["cardNumber"] = keeperCard, ["cardExpirationDate"] = "03/2030", ["cardSecurityCode"] = "9871" },
            },
        };
        JsonObject bitwardenLogin = BitwardenLogin("bw-github", "GitHub", "alice", NewPassword, MaskedUri);
        bitwardenLogin["login"]!["totp"] = bitwardenTotp;
        bitwardenLogin["notes"] = BitwardenNotes;
        bitwardenLogin["fields"] = new JsonArray(new JsonObject { ["name"] = "API key", ["value"] = HiddenValue, ["type"] = 1 });
        bitwardenLogin["passwordHistory"] = new JsonArray(new JsonObject { ["lastUsedDate"] = "2025-01-01T00:00:00.000Z", ["password"] = HistoryPassword });
        var bitwardenSsh = new JsonObject
        {
            ["id"] = "bw-ssh",
            ["type"] = 5,
            ["name"] = "Deploy key",
            ["sshKey"] = new JsonObject { ["privateKey"] = bitwardenKey, ["publicKey"] = PemFixtures.Ed25519PublicKey, ["keyFingerprint"] = "SHA256:fixture" },
        };
        var bitwardenCardItem = new JsonObject
        {
            ["id"] = "bw-card",
            ["type"] = 3,
            ["name"] = "Fixture card",
            ["card"] = new JsonObject { ["cardholderName"] = "Alice Example", ["number"] = bitwardenCard, ["code"] = "4567", ["expMonth"] = "12", ["expYear"] = "2030" },
        };
        string zip = Write(directory, "keeper.zip", CommanderZip(
            KeeperJson(
                KeeperLogin("rec-github", "GitHub", "alice", OldPassword, "https://github.com/login", keeperNotes, keeperTotp),
                KeeperSshKey("rec-ssh", "Deploy key", keeperKey, PemFixtures.Ed25519PublicKey),
                keeperCardRecord,
                KeeperFileRecord("rec-files", "GitHub backup", "file-codes", "github-recovery-codes.txt")),
            ("files/file-codes", string.Join('\n', fileCodes))));
        string bitwarden = Write(directory, "bitwarden.json", BitwardenJson(bitwardenLogin, bitwardenSsh, bitwardenCardItem));
        string briefPath = Path.Combine(directory, "brief.json");
        string[] args =
        [
            "merge", zip, bitwarden, "--output", Path.Combine(directory, "merged.json"), "--decisions", Path.Combine(directory, "decisions.json"),
            "--brief", briefPath, "--policy", "ask",
        ];

        var run = await CommandLineRun.Invoke(new TestPrompts(ExportPassword).Environment(), args, TestContext.Current.CancellationToken);

        run.ExitCode.ShouldBe(2);
        string json = await File.ReadAllTextAsync(briefPath, TestContext.Current.CancellationToken);
        var brief = BriefFile.Deserialize(json);
        string[] fieldPaths = [.. brief.Groups.SelectMany(group => group.Fields).Select(field => field.FieldPath)];
        string[] expectedFields =
        [
            FieldPaths.LoginPassword, FieldPaths.LoginTotp, FieldPaths.Notes, FieldPaths.Fields, FieldPaths.PasswordHistory, FieldPaths.LoginUris,
            "sshKey.privateKey", "card.number", "card.code",
        ];
        expectedFields.ShouldAllSatisfy(fieldPath => fieldPaths.ShouldContain(fieldPath));
        brief.Groups.SelectMany(group => group.Members).SelectMany(member => member.Findings).ShouldNotBeEmpty();
        string[] values =
        [
            OldPassword, NewPassword, NotesPassword, HistoryPassword, HiddenValue, BitwardenNotes, keeperNotes, keeperTotp, keeperSecret, bitwardenTotp,
            bitwardenSecret, keeperCard, bitwardenCard, "9871", "4567", "fixture-uri-secret", "fixture-uri-token", "fixture-uri-fragment", .. notesCodes,
            .. fileCodes, .. keeperKey.Split('\n').Where(line => line.Length > 0 && !line.StartsWith('-')),
            .. bitwardenKey.Split('\n').Where(line => line.Length > 0 && !line.StartsWith('-')),
        ];
        string[] texts = [json, run.Output, run.Error];
        texts.ShouldAllSatisfy(text => values.ShouldAllSatisfy(value => text.ShouldNotContain(value, StringComparison.Ordinal)));
    }
}
