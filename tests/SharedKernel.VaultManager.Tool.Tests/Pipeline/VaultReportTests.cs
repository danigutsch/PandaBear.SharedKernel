using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Secrets;
using static PandaBear.VaultManager.Tool.Tests.Pipeline.PipelineInputs;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

public sealed class VaultReportTests : IDisposable
{
    private const string OldPassword = "fixture-old-keeper-password";
    private const string NewPassword = "fixture-new-bitwarden-password";
    private const string NotesPassword = "Gh-notes-Passw0rd!";
    private const string KeeperOnlyPassword = "fixture-keeper-only-password";
    private const string ExportPassword = "fixture export password";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-pipeline-report-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void Report_decisions_file_and_result_text_never_contain_vault_values()
    {
        string totpSecret = PipelineSecrets.TotpSecret(600);
        string totp = PipelineSecrets.TotpUri(totpSecret);
        string[] notesCodes = PipelineSecrets.GitHubRecoveryCodes(610);
        string[] fileCodes = PipelineSecrets.GitHubRecoveryCodes(630);
        string privateKey = PemFixtures.Ed25519OpenSsh();
        string zip = Write(directory, "keeper.zip", CommanderZip(
            KeeperJson(
                KeeperLogin("rec-github", "GitHub", "alice", OldPassword, "https://github.com/login", PipelineSecrets.RecoveryNotes(notesCodes, NotesPassword), totp),
                KeeperLogin("rec-keeper", "Keeper only", "carol", KeeperOnlyPassword, "https://keeper-only.example.com"),
                KeeperSshKey("rec-ssh", "Deploy key", privateKey, PemFixtures.Ed25519PublicKey),
                KeeperFileRecord("rec-files", "GitHub backup", "file-codes", "github-recovery-codes.txt")),
            ("files/file-codes", string.Join('\n', fileCodes))));
        string bitwarden = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-github", "GitHub", "alice", NewPassword, "https://github.com")));
        string output = Path.Combine(directory, "merged.json");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());
        var proposed = pipeline.Run(new PipelineRequest([zip, bitwarden]));
        var github = proposed.Findings.ShouldHaveSingleItem(item => string.Equals(item.ItemName, "GitHub", StringComparison.Ordinal));
        var group = proposed.Match.Groups.ShouldHaveSingleItem(candidate => candidate.Members.Count == 2);
        var decisions = PipelineDecisions.Findings(github.OriginKey, [.. github.Findings.Select(finding => new FindingChoice(finding.Id, FindingDecision.Accepted))]) with
        {
            Groups = [new GroupDecision(group.Key, [FieldChoice.Member(FieldPaths.LoginPassword, group.MemberKeys[1])])],
            Overrides = new MatchOverrides([new OriginKeyPair(group.MemberKeys[0], group.MemberKeys[1])], []),
        };

        var result = pipeline.Run(new PipelineRequest([zip, bitwarden]) { Decisions = decisions, Output = new PipelineOutput(output) });

        result.OutputWritten.ShouldBeTrue();
        string report = VaultReport.ToMarkdown(result);
        string decisionsFile = DecisionsFile.Serialize(decisions);
        string printed = result.ToString();
        string[] values =
        [
            OldPassword, NewPassword, NotesPassword, KeeperOnlyPassword, ExportPassword, totp, totpSecret,
            .. notesCodes, .. fileCodes, .. privateKey.Split('\n').Where(line => !line.StartsWith('-')),
        ];
        string[] texts = [report, decisionsFile, printed];
        texts.ShouldAllSatisfy(text => values.ShouldAllSatisfy(value => text.ShouldNotContain(value, StringComparison.Ordinal)));
        report.ShouldContain("GitHub backup", StringComparison.Ordinal);
        report.ShouldContain("github-recovery-codes.txt", StringComparison.Ordinal);
        report.ShouldContain("`" + github.OriginKey.Replace("|", "\\|", StringComparison.Ordinal) + "`", StringComparison.Ordinal);
        report.ShouldContain("`" + github.Findings[0].Id + "` | yes |", StringComparison.Ordinal);
    }

    [Fact]
    public void Report_combines_merge_findings_password_health_uploads_and_warnings()
    {
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(700);
        string zip = Write(directory, "keeper.zip", CommanderZip(
            KeeperJson(
                KeeperLogin("rec-mail", "Mail", "alice", "short", "https://mail.example.com"),
                KeeperLogin("rec-bank", "Bank", "alice", "short", "https://bank.example.org"),
                KeeperFileRecord("rec-files", "GitHub backup", "file-codes", "github-recovery-codes.txt")),
            ("files/file-codes", string.Join('\n', codes))));
        string csv = Write(
            directory,
            "bitwarden.csv",
            "folder,favorite,type,name,notes,fields,reprompt,login_uri,login_username,login_password,login_totp\n"
            + ",,login,Mail,,,0,https://mail.example.com,alice,other-mail-password,\n");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([zip, csv]) { Output = new PipelineOutput(Path.Combine(directory, "merged.json")) });

        string report = VaultReport.ToMarkdown(result);
        result.HasUnresolvedConflicts.ShouldBeTrue();
        report.ShouldStartWith("# Vault report\n\nThe merged vault was not written because 1 field(s) need a decision.", StringComparison.Ordinal);
        report.ShouldContainInOrder(
            "## Merge report",
            "| Unique | 2 |",
            "## Secret findings",
            "1 of 1 finding(s) were accepted.",
            "| GitHub backup | RecoveryCodes |",
            "## Password health",
            "| Bank | reused on other sites | Mail |",
            "| Bank | shorter than 12 characters |",
            "## Attachments to upload",
            "| GitHub backup | github-recovery-codes.txt | keeper.zip, `files/file-codes` |",
            "## Warnings",
            "- bitwarden.csv: Bitwarden CSV exports omit");
        report.ShouldNotContain("other-mail-password", StringComparison.Ordinal);
        report.ShouldNotContain("\r", StringComparison.Ordinal);
    }
}
