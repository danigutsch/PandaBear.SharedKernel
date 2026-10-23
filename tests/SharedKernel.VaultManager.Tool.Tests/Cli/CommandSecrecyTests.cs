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
}
