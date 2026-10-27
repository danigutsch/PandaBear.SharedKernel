using System.Text.Json.Nodes;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Secrets;
using static PandaBear.VaultManager.Tool.Tests.Pipeline.PipelineInputs;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

/// <summary>Tests the Markdown vault report produced from a pipeline result.</summary>
public sealed class VaultReportTests : IDisposable
{
    private const string OldPassword = "fixture-old-keeper-password";
    private const string NewPassword = "fixture-new-bitwarden-password";
    private const string NotesPassword = "Gh-notes-Passw0rd!";
    private const string KeeperOnlyPassword = "fixture-keeper-only-password";
    private const string ExportPassword = "fixture export password";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-pipeline-report-").FullName;

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    /// <summary>The report, the decisions file, and the printed result never contain a vault value such as a password, TOTP secret, recovery code, or private key.</summary>
    [Fact]
    public void Report_decisions_file_and_result_text_never_contain_vault_values()
    {
        // Arrange
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

        // Act
        var result = pipeline.Run(new PipelineRequest([zip, bitwarden]) { Decisions = decisions, Output = new PipelineOutput(output) });
        string report = VaultReport.ToMarkdown(result);
        string decisionsFile = DecisionsFile.Serialize(decisions);
        string printed = result.ToString();

        // Assert
        result.OutputWritten.ShouldBeTrue();
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

    /// <summary>The report combines the merge summary, secret findings, password health, attachments to upload, and warnings, and starts with why the vault was not written.</summary>
    [Fact]
    public void Report_combines_merge_findings_password_health_uploads_and_warnings()
    {
        // Arrange
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

        // Act
        var result = pipeline.Run(new PipelineRequest([zip, csv]) { Output = new PipelineOutput(Path.Combine(directory, "merged.json")) });
        string report = VaultReport.ToMarkdown(result);

        // Assert
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
            "| Bank | weak |",
            "## Attachments to upload",
            "| GitHub backup | github-recovery-codes.txt | keeper.zip, `files/file-codes` |",
            "## Warnings",
            "- bitwarden.csv: Bitwarden CSV exports omit");
        report.ShouldNotContain("other-mail-password", StringComparison.Ordinal);
        report.ShouldNotContain("\r", StringComparison.Ordinal);
    }

    /// <summary>The report status counts the fields and findings that still need a decision.</summary>
    [Theory]
    [InlineData(false, "0 field(s) and 1 finding(s)")]
    [InlineData(true, "1 field(s) and 1 finding(s)")]
    public void Report_status_names_the_fields_and_the_findings_that_need_a_decision(bool conflict, string counts)
    {
        // Arrange
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(720);
        string keeper = Write(directory, "keeper.json", KeeperJson(
            KeeperLogin("rec-github", "GitHub", "alice", OldPassword, "https://github.com", PipelineSecrets.RecoveryNotes(codes, NotesPassword), modified: !conflict)));
        string bitwarden = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-github", "GitHub", "alice", NewPassword, "https://github.com", revised: false)));
        string[] inputs = conflict ? [keeper, bitwarden] : [keeper];
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest(inputs) { Output = new PipelineOutput(Path.Combine(directory, "merged.json")) });
        string report = VaultReport.ToMarkdown(result);

        // Assert
        report.ShouldStartWith("# Vault report\n\nThe merged vault was not written because " + counts + " need a decision.", StringComparison.Ordinal);
    }

    /// <summary>The report lists the files of attachment folders shared by two items named Bank so they can be uploaded by hand.</summary>
    [Fact]
    public void Report_lists_the_files_of_attachment_folders_that_two_items_named_bank_share_for_upload_by_hand()
    {
        // Arrange
        string zip = Write(directory, "bitwarden.zip", TwoBanksZip());
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([zip]));
        string report = VaultReport.ToMarkdown(result);

        // Assert
        report.ShouldNotContain("No item has attachments.", StringComparison.Ordinal);
        report.ShouldContainInOrder(
            "## Attachments to upload",
            "| Folder | File | Source | Matching items |",
            "| Bank | statement.txt | bitwarden.zip, `attachments/Bank/statement.txt` | 2 |",
            "| Bank_1 | contract.txt | bitwarden.zip, `attachments/Bank_1/contract.txt` | 2 |");
    }

    /// <summary>The report replaces control characters in item names, folder names, file names, and warnings.</summary>
    [Fact]
    public void Report_replaces_control_characters_in_item_names_folder_names_file_names_and_warnings()
    {
        // Arrange
        const string Name = "Bank\u001b[2J\u009b31m";
        const string Folder = "Finance\u001b]0;title\u0007\u007f";
        const string FileName = "scan\u001b[5m.pdf";
        var keeperBank = KeeperLogin("rec-bank", Name, "alice", "fixture-keeper-bank-password", "https://bank.example.com", notes: "PIN: 4821");
        keeperBank["folders"] = new JsonArray(new JsonObject { ["folder"] = Folder });
        string keeper = Write(directory, "keeper.zip", CommanderZip(KeeperJson(keeperBank, KeeperFileRecord("rec-scan", Name, "file-scan", FileName))));
        var bitwardenBank = BitwardenLogin("bw-bank", Name, "alice", "fixture-bitwarden-bank-password", "https://bank.example.com");
        bitwardenBank["folderId"] = "folder-imported";
        var bitwardenExport = new JsonObject
        {
            ["encrypted"] = false,
            ["folders"] = new JsonArray(new JsonObject { ["id"] = "folder-imported", ["name"] = "Imported" }),
            ["items"] = new JsonArray(bitwardenBank),
        };
        string bitwarden = Write(directory, "bitwarden.json", bitwardenExport.ToJsonString());
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper, bitwarden]));
        string report = VaultReport.ToMarkdown(result);

        // Assert
        report.ToCharArray().Where(char.IsControl).ShouldAllSatisfy(character => character.ShouldBe('\n'));
        report.ShouldContain("| Bank\uFFFD[2J\uFFFD31m |", StringComparison.Ordinal);
        report.ShouldContain("Folder \"Finance\uFFFD]0;title\uFFFD\uFFFD\" from keeper.zip was not used", StringComparison.Ordinal);
        report.ShouldContain("| scan\uFFFD[5m.pdf |", StringComparison.Ordinal);
        report.ShouldContain("attachment \"scan\uFFFD[5m.pdf\" is listed but not included in the zip.", StringComparison.Ordinal);
    }

    /// <summary>A changed group whose item name holds a line separator stays on its own heading line and injects no extra heading.</summary>
    [Fact]
    public void Report_keeps_a_changed_group_whose_item_name_holds_a_line_separator_on_its_heading_line()
    {
        // Arrange
        const string Name = "Bank\u2028# Injected heading";
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperLogin("rec-bank", Name, "alice", "fixture-keeper-bank-password", "https://bank.example.com")));
        string bitwarden = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-bank", Name, "alice", "fixture-bitwarden-bank-password", "https://bank.example.com")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper, bitwarden]));
        string report = VaultReport.ToMarkdown(result);

        // Assert
        string[] headings = [.. report.Split('\n').Where(line => line.StartsWith('#'))];
        headings.ShouldContain(heading => heading.StartsWith("#### Bank # Injected heading (`", StringComparison.Ordinal));
        headings.ShouldNotContain(heading => heading.Contains("Injected", StringComparison.Ordinal) && !heading.StartsWith("#### Bank", StringComparison.Ordinal));
        report.ShouldNotContain("\u2028", StringComparison.Ordinal);
    }

    /// <summary>A password equal to the item name or user name is listed only as weak, without naming that reason.</summary>
    [Fact]
    public void Report_lists_a_password_equal_to_the_item_name_or_user_name_only_as_weak()
    {
        // Arrange
        string input = Write(directory, "bitwarden.json", BitwardenJson(
            BitwardenLogin("bw-router", "Router-Admin-Panel", "admin", "Router-Admin-Panel", "http://192.168.1.1"),
            BitwardenLogin("bw-printer", "Printer", "printer-admin-01", "printer-admin-01", "http://192.168.1.2")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([input]));
        string report = VaultReport.ToMarkdown(result);

        // Assert
        report.ShouldContainInOrder("## Password health", "| Router-Admin-Panel | weak |", "| Printer | weak |");
        report.ShouldNotContain("same as the", StringComparison.Ordinal);
        result.PasswordHealth.Issues.ShouldAllSatisfy(issue => issue.ToString().ShouldNotContain("Equals", StringComparison.Ordinal));
    }

    /// <summary>A short single-class password equal to the user name is listed once as weak without naming any reason.</summary>
    [Fact]
    public void Report_lists_a_short_single_class_password_equal_to_the_user_name_once_without_naming_the_reason()
    {
        // Arrange
        string input = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-router", "Router", "admin", "admin", "http://192.168.1.1")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([input]));
        string report = VaultReport.ToMarkdown(result);

        // Assert
        report.Split('\n').ShouldHaveSingleItem(line => line.StartsWith("| Router |", StringComparison.Ordinal)).ShouldBe("| Router | weak |  |");
        string[] reasons = ["shorter than", "kind of character", "guessed", "same as", "user name"];
        string health = report[report.IndexOf("## Password health", StringComparison.Ordinal)..report.IndexOf("## Attachments to upload", StringComparison.Ordinal)];
        reasons.ShouldAllSatisfy(reason => health.ShouldNotContain(reason, StringComparison.Ordinal));
    }

    /// <summary>The report names up to three other items that reuse a password and counts the rest as "and N more".</summary>
    [Fact]
    public void Report_names_three_other_items_that_reuse_a_password_and_counts_the_rest()
    {
        // Arrange
        string input = Write(directory, "bitwarden.json", BitwardenJson(
            BitwardenLogin("bw-mail", "Mail", "alice", KeeperOnlyPassword, "https://mail.example.com"),
            BitwardenLogin("bw-bank", "Bank", "alice", KeeperOnlyPassword, "https://bank.example.com"),
            BitwardenLogin("bw-shop", "Shop", "alice", KeeperOnlyPassword, "https://shop.example.com"),
            BitwardenLogin("bw-news", "News", "alice", KeeperOnlyPassword, "https://news.example.com"),
            BitwardenLogin("bw-chat", "Chat", "alice", KeeperOnlyPassword, "https://chat.example.com")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([input]));
        string report = VaultReport.ToMarkdown(result);

        // Assert
        report.ShouldContainInOrder(
            "## Password health",
            "| Mail | reused on other sites | Bank, Shop, News, and 1 more |",
            "| Chat | reused on other sites | Mail, Bank, Shop, and 1 more |");
        report.ShouldNotContain(KeeperOnlyPassword, StringComparison.Ordinal);
    }
}
