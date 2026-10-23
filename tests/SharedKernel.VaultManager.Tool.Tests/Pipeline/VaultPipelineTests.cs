using System.Security.Cryptography;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Secrets;
using PandaBear.VaultManager.Tool.Tests.Secrets;
using static PandaBear.VaultManager.Tool.Tests.Pipeline.PipelineInputs;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

public sealed class VaultPipelineTests : IDisposable
{
    private const string OldPassword = "fixture-old-keeper-password";
    private const string NewPassword = "fixture-new-bitwarden-password";
    private const string NotesPassword = "Gh-notes-Passw0rd!";
    private const string ExportPassword = "fixture export password";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-pipeline-tests-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void Run_keeps_the_newer_bitwarden_password_the_keeper_totp_and_the_old_password_in_history()
    {
        string totp = PipelineSecrets.TotpUri(PipelineSecrets.TotpSecret(300));
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperLogin("rec-github", "GitHub", "alice", OldPassword, "https://github.com/login", totp: totp)));
        string bitwarden = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-github", "GitHub", "alice", NewPassword, "https://github.com")));
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword);
        var pipeline = new VaultPipeline(prompts.Environment());

        var result = pipeline.Run(new PipelineRequest([keeper, bitwarden]) { Output = new PipelineOutput(output) });

        result.OutputWritten.ShouldBeTrue();
        result.HasUnresolvedConflicts.ShouldBeFalse();
        result.HasWriteErrors.ShouldBeFalse();
        prompts.NewPasswordPrompts.ShouldBe([VaultPipeline.NewExportPasswordPrompt]);
        string json = PasswordProtectedExport.Decrypt(File.ReadAllText(output), ExportPassword);
        var item = BitwardenJsonReader.Read(json, "merged.json").Items.ShouldHaveSingleItem();
        var login = item.Login.ShouldNotBeNull();
        login.Password.ShouldBe(NewPassword);
        login.Totp.ShouldBe(totp);
        var history = item.PasswordHistory.ShouldHaveSingleItem();
        history.Password.ShouldBe(OldPassword);
        history.LastUsedDate.ShouldBe(KeeperModified);
    }

    [Fact]
    public void Run_places_pre_accepted_recovery_codes_and_leaves_the_proposed_notes_password_in_notes()
    {
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(400);
        string keeper = Write(directory, "keeper.json", KeeperJson(
            KeeperLogin("rec-github", "GitHub", "alice", url: "https://github.com", notes: PipelineSecrets.RecoveryNotes(codes, NotesPassword))));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([keeper]));

        var findings = result.Findings.ShouldHaveSingleItem();
        var codesFinding = findings.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.RecoveryCodes);
        var passwordFinding = findings.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Password);
        codesFinding.IsPreAccepted.ShouldBeTrue();
        passwordFinding.IsPreAccepted.ShouldBeFalse();
        findings.AcceptedFindingIds.ShouldBe([codesFinding.Id]);
        var item = result.Merged.Items.ShouldHaveSingleItem();
        var field = item.Fields.ShouldHaveSingleItem();
        field.Name.ShouldBe("Recovery codes");
        field.Type.ShouldBe(CustomFieldType.Hidden);
        field.Value.ShouldBe(string.Join('\n', codes));
        var password = item.Login.ShouldNotBeNull().Password;
        password.ShouldBeNull();
        string notes = item.Notes.ShouldNotBeNull();
        notes.ShouldContain("Password: " + NotesPassword, StringComparison.Ordinal);
        codes.ShouldAllSatisfy(code => notes.ShouldNotContain(code, StringComparison.Ordinal));
    }

    [Fact]
    public void Run_moves_recovery_codes_and_an_accepted_notes_password_into_their_fields()
    {
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(410);
        string keeper = Write(directory, "keeper.json", KeeperJson(
            KeeperLogin("rec-github", "GitHub", "alice", url: "https://github.com", notes: PipelineSecrets.RecoveryNotes(codes, NotesPassword))));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());
        var findings = pipeline.Run(new PipelineRequest([keeper])).Findings.ShouldHaveSingleItem();
        var passwordFinding = findings.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Password);
        var decisions = PipelineDecisions.Findings(findings.OriginKey, new FindingChoice(passwordFinding.Id, FindingDecision.Accepted));

        var result = pipeline.Run(new PipelineRequest([keeper]) { Decisions = decisions });

        var item = result.Merged.Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Password.ShouldBe(NotesPassword);
        var field = item.Fields.ShouldHaveSingleItem();
        field.Name.ShouldBe("Recovery codes");
        field.Type.ShouldBe(CustomFieldType.Hidden);
        field.Value.ShouldBe(string.Join('\n', codes));
        item.Notes.ShouldBeNull();
        result.PlacementProblems.ShouldBeEmpty();
    }

    [Fact]
    public void Run_skips_a_finding_rejected_in_decisions_and_applies_a_proposed_one_accepted_there()
    {
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(420);
        string keeper = Write(directory, "keeper.json", KeeperJson(
            KeeperLogin("rec-github", "GitHub", "alice", url: "https://github.com", notes: PipelineSecrets.RecoveryNotes(codes, NotesPassword))));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());
        var findings = pipeline.Run(new PipelineRequest([keeper])).Findings.ShouldHaveSingleItem();
        var codesFinding = findings.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.RecoveryCodes);
        var passwordFinding = findings.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Password);
        var decisions = PipelineDecisions.Findings(
            findings.OriginKey,
            new FindingChoice(codesFinding.Id, FindingDecision.Rejected),
            new FindingChoice(passwordFinding.Id, FindingDecision.Accepted));

        var result = pipeline.Run(new PipelineRequest([keeper]) { Decisions = decisions });

        result.Findings.ShouldHaveSingleItem().AcceptedFindingIds.ShouldBe([passwordFinding.Id]);
        var item = result.Merged.Items.ShouldHaveSingleItem();
        item.Fields.ShouldBeEmpty();
        item.Login.ShouldNotBeNull().Password.ShouldBe(NotesPassword);
        string notes = item.Notes.ShouldNotBeNull();
        codes.ShouldAllSatisfy(code => notes.ShouldContain(code, StringComparison.Ordinal));
        notes.ShouldNotContain(NotesPassword, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_passes_through_items_found_in_only_one_input()
    {
        string keeper = Write(directory, "keeper.json", KeeperJson(
            KeeperLogin("rec-keeper", "Keeper only", "carol", "fixture-keeper-only-password", "https://keeper-only.example.com")));
        string bitwarden = Write(directory, "bitwarden.json", BitwardenJson(
            BitwardenLogin("bw-only", "Bitwarden only", "dave", "fixture-bitwarden-only-password", "https://bitwarden-only.example.com")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([keeper, bitwarden]));

        result.Merged.Items.Select(item => item.Name).ShouldBe(["Keeper only", "Bitwarden only"]);
        result.Merged.Items[0].Login.ShouldNotBeNull().Password.ShouldBe("fixture-keeper-only-password");
        result.Merged.Items[1].Login.ShouldNotBeNull().Password.ShouldBe("fixture-bitwarden-only-password");
        result.MergeReport.UniqueGroups.ShouldBe(2);
        result.HasUnresolvedConflicts.ShouldBeFalse();
    }

    [Fact]
    public void Run_completes_a_keeper_ssh_key_that_has_no_fingerprint()
    {
        string publicKey = PemFixtures.Ed25519PublicKey + " deploy@example";
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperSshKey("rec-ssh", "Deploy key", PemFixtures.Ed25519OpenSsh(), publicKey)));
        string output = Path.Combine(directory, "merged.json");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([keeper]) { Output = new PipelineOutput(output, Plaintext: true) });

        result.OutputWritten.ShouldBeTrue();
        var item = BitwardenJsonReader.Read(File.ReadAllText(output), "merged.json").Items.ShouldHaveSingleItem();
        item.Type.ShouldBe(VaultItemType.SshKey);
        var key = item.SshKey.ShouldNotBeNull();
        key.KeyFingerprint.ShouldBe(PemFixtures.Ed25519Fingerprint);
        key.PublicKey.ShouldBe(publicKey);
        key.PrivateKey.ShouldBe(PemFixtures.Ed25519OpenSsh());
    }

    [Fact]
    public void Run_writes_a_keeper_ssh_key_whose_body_is_not_base64_as_a_secure_note_that_keeps_the_key()
    {
        using var rsa = RSA.Create(2048);
        string privateKey = PemFixtures.WithBrokenBody(PemFixtures.RsaPkcs1(rsa), shortenLine: false);
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperSshKey("rec-ssh", "Deploy key", privateKey, string.Empty)));
        string output = Path.Combine(directory, "merged.json");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([keeper]) { Output = new PipelineOutput(output, Plaintext: true) });

        result.OutputWritten.ShouldBeTrue();
        var item = BitwardenJsonReader.Read(File.ReadAllText(output), "merged.json").Items.ShouldHaveSingleItem();
        item.Name.ShouldBe("Deploy key");
        item.Type.ShouldBe(VaultItemType.SecureNote);
        item.Fields.ShouldBe([new CustomField(SshKeyNormalizer.PrivateKeyField, privateKey, CustomFieldType.Hidden)]);
    }

    [Fact]
    public void Run_writes_a_keeper_ssh_key_with_explicit_curve_parameters_as_a_secure_note_that_keeps_the_key()
    {
        using var named = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var explicitCurve = ECDsa.Create(named.ExportExplicitParameters(includePrivateParameters: true));
        string privateKey = PemFixtures.Sec1(explicitCurve);
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperSshKey("rec-ssh", "Deploy key", privateKey, string.Empty)));
        string output = Path.Combine(directory, "merged.json");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([keeper]) { Output = new PipelineOutput(output, Plaintext: true) });

        result.OutputWritten.ShouldBeTrue();
        var item = BitwardenJsonReader.Read(File.ReadAllText(output), "merged.json").Items.ShouldHaveSingleItem();
        item.Type.ShouldBe(VaultItemType.SecureNote);
        item.Fields.ShouldBe([new CustomField(SshKeyNormalizer.PrivateKeyField, privateKey, CustomFieldType.Hidden)]);
    }

    [Fact]
    public void Run_writes_text_stored_around_a_keeper_ssh_key_to_a_hidden_field_of_the_key_item()
    {
        const string Label = "deploy key for git.example.com";
        const string HostLine = "host: prod-db-1";
        string stored = Label + "\n" + PemFixtures.Ed25519OpenSsh() + "\n" + HostLine;
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperSshKey("rec-ssh", "Deploy key", stored, string.Empty)));
        string output = Path.Combine(directory, "merged.json");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([keeper]) { Output = new PipelineOutput(output, Plaintext: true) });

        result.OutputWritten.ShouldBeTrue();
        var item = BitwardenJsonReader.Read(File.ReadAllText(output), "merged.json").Items.ShouldHaveSingleItem();
        item.SshKey.ShouldNotBeNull().PrivateKey.ShouldBe(PemFixtures.Ed25519OpenSsh());
        item.Fields.ShouldBe([new CustomField(SshKeyNormalizer.OtherTextField, Label + "\n" + HostLine, CustomFieldType.Hidden)]);
    }

    [Fact]
    public void Run_scans_recovery_codes_in_a_commander_zip_attachment_and_lists_the_file_for_upload()
    {
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(500);
        string zip = Write(directory, "keeper.zip", CommanderZip(
            KeeperJson(KeeperFileRecord("rec-files", "GitHub backup", "file-codes", "github-recovery-codes.txt")),
            ("files/file-codes", string.Join('\n', codes) + "\n")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([zip]));

        var findings = result.Findings.ShouldHaveSingleItem();
        var finding = findings.Findings.ShouldHaveSingleItem();
        finding.Kind.ShouldBe(SecretKind.RecoveryCodes);
        finding.Location.FieldPath.ShouldBe(SecretLocation.Attachment("github-recovery-codes.txt"));
        findings.AcceptedFindingIds.ShouldBe([finding.Id]);
        var item = result.Merged.Items.ShouldHaveSingleItem();
        var field = item.Fields.ShouldHaveSingleItem();
        field.Name.ShouldBe("Recovery codes");
        field.Value.ShouldBe(string.Join('\n', codes));
        item.Attachments.ShouldHaveSingleItem().FileName.ShouldBe("github-recovery-codes.txt");
        result.ManualAttachments.ShouldBe([new ManualAttachment("GitHub backup", "github-recovery-codes.txt", "keeper.zip", "files/file-codes")]);
    }

    [Fact]
    public void Run_lists_attachments_missing_from_the_export_for_download_from_the_source_vault()
    {
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperFileRecord("rec-files", "GitHub backup", "file-codes", "github-recovery-codes.txt")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([keeper]));

        result.Findings.ShouldBeEmpty();
        result.ManualAttachments.ShouldBe([new ManualAttachment("GitHub backup", "github-recovery-codes.txt", "keeper.json", null)]);
    }

    [Fact]
    public void Run_lists_two_attachments_with_one_file_name_on_one_keeper_item_separately()
    {
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperFileRecord("rec-files", "Scans", ("file-one", "scan.pdf"), ("file-two", "scan.pdf"))));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([keeper]));

        result.ManualAttachments.ShouldBe(
        [
            new ManualAttachment("Scans", "scan.pdf", "keeper.json", null),
            new ManualAttachment("Scans", "scan.pdf", "keeper.json", null),
        ]);
    }

    [Fact]
    public void Run_lists_two_files_with_one_name_and_size_on_one_commander_item_separately()
    {
        string zip = Write(directory, "keeper.zip", CommanderZip(
            KeeperJson(KeeperFileRecord("rec-files", "Scans", ("file-one", "scan.txt"), ("file-two", "scan.txt"))),
            ("files/file-one", "first scan text\n"),
            ("files/file-two", "other scan text\n")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([zip]));

        result.ManualAttachments.ShouldBe(
        [
            new ManualAttachment("Scans", "scan.txt", "keeper.zip", "files/file-one"),
            new ManualAttachment("Scans", "scan.txt", "keeper.zip", "files/file-two"),
        ]);
    }

    [Fact]
    public void Run_lists_a_file_found_on_two_merged_items_once()
    {
        string zip = Write(directory, "keeper.zip", CommanderZip(
            KeeperJson(
                WithAttachments(KeeperLogin("rec-one", "GitHub", "alice", OldPassword, "https://github.com"), ("file-a", "codes.txt"), ("file-b", "codes.txt")),
                WithAttachments(KeeperLogin("rec-two", "GitHub", "alice", OldPassword, "https://github.com"), ("file-c", "codes.txt"))),
            ("files/file-a", "first codes\n"),
            ("files/file-b", "other codes\n"),
            ("files/file-c", "first codes\n")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([zip]));

        result.Match.Groups.ShouldHaveSingleItem().Members.Count.ShouldBe(2);
        result.ManualAttachments.ShouldBe(
        [
            new ManualAttachment("GitHub", "codes.txt", "keeper.zip", "files/file-a"),
            new ManualAttachment("GitHub", "codes.txt", "keeper.zip", "files/file-b"),
        ]);
    }

    [Fact]
    public void Run_lists_files_with_one_name_and_size_but_different_content_on_two_merged_items_separately()
    {
        var older = WithAttachments(KeeperLogin("rec-old", "GitHub", "alice", OldPassword, "https://github.com"), ("file-old", "github-recovery-codes.txt"));
        var newer = WithAttachments(KeeperLogin("rec-new", "GitHub", "alice", OldPassword, "https://github.com"), ("file-new", "github-recovery-codes.txt"));
        newer["last_modified"] = KeeperModified.AddDays(30).ToUnixTimeSeconds();
        string zip = Write(directory, "keeper.zip", CommanderZip(
            KeeperJson(older, newer),
            ("files/file-old", "old1-aaaa old2-bbbb\n"),
            ("files/file-new", "new1-cccc new2-dddd\n")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([zip]));

        result.Match.Groups.ShouldHaveSingleItem().Members.Count.ShouldBe(2);
        result.ManualAttachments.ShouldBe(
        [
            new ManualAttachment("GitHub", "github-recovery-codes.txt", "keeper.zip", "files/file-new"),
            new ManualAttachment("GitHub", "github-recovery-codes.txt", "keeper.zip", "files/file-old"),
        ]);
    }

    [Fact]
    public void Run_lists_a_file_found_in_two_exports_once_from_the_newest_item()
    {
        byte[] scan = [.. "%PDF-1.7\n%synthetic scan\n"u8];
        string keeper = Write(directory, "keeper.zip", TestZip.Create(
            ("export.json", TestZip.Utf8(KeeperJson(WithAttachments(KeeperLogin("rec-github", "GitHub", "alice", OldPassword, "https://github.com"), ("file-scan", "scan.pdf"))))),
            ("files/file-scan", scan)));
        string bitwarden = Write(directory, "bitwarden.zip", TestZip.Create(
            ("data.json", TestZip.Utf8(BitwardenJson(BitwardenLogin("bw-github", "GitHub", "alice", NewPassword, "https://github.com")))),
            ("attachments/GitHub/scan.pdf", scan)));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([keeper, bitwarden]));

        result.Match.Groups.ShouldHaveSingleItem().Members.Count.ShouldBe(2);
        result.ManualAttachments.ShouldBe([new ManualAttachment("GitHub", "scan.pdf", "bitwarden.zip", "attachments/GitHub/scan.pdf")]);
    }

    [Fact]
    public void Run_lists_same_named_attachments_missing_from_the_export_on_two_merged_items_separately()
    {
        string keeper = Write(directory, "keeper.json", KeeperJson(
            WithAttachments(KeeperLogin("rec-one", "GitHub", "alice", OldPassword, "https://github.com"), ("file-one", "scan.pdf")),
            WithAttachments(KeeperLogin("rec-two", "GitHub", "alice", OldPassword, "https://github.com"), ("file-two", "scan.pdf"))));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([keeper]));

        result.Match.Groups.ShouldHaveSingleItem().Members.Count.ShouldBe(2);
        result.ManualAttachments.ShouldBe(
        [
            new ManualAttachment("GitHub", "scan.pdf", "keeper.json", null),
            new ManualAttachment("GitHub", "scan.pdf", "keeper.json", null),
        ]);
    }

    [Fact]
    public void Run_keeps_origin_keys_unique_when_an_input_ID_equals_the_key_of_a_created_item()
    {
        string notes = "deploy key\n" + PemFixtures.Ed25519OpenSsh();
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperNote("rec-server", "Server", notes)));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());
        string findingId = pipeline.Run(new PipelineRequest([keeper])).Findings.ShouldHaveSingleItem().Findings.ShouldHaveSingleItem().Id;
        string createdId = "rec-server/" + findingId;
        Write(directory, "keeper.json", KeeperJson(KeeperNote("rec-server", "Server", notes), KeeperNote(createdId, "Other", "plain note")));

        var result = pipeline.Run(new PipelineRequest([keeper]));

        string[] keys = [.. result.Match.Groups.SelectMany(group => group.MemberKeys)];
        keys.ShouldBe(["keeper|keeper.json|rec-server", "keeper|keeper.json|" + createdId + "#2", "keeper|keeper.json|" + createdId]);
        var created = result.Merged.Items.ShouldHaveSingleItem(item => item.Type == VaultItemType.SshKey);
        created.Origin.OriginalId.ShouldBe(createdId + "#2");
    }

    [Fact]
    public void Merge_applies_new_decisions_to_a_scanned_vault_without_reading_the_inputs_again()
    {
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(800);
        string keeper = Write(directory, "keeper.json", KeeperJson(
            KeeperLogin("rec-github", "GitHub", "alice", url: "https://github.com", notes: PipelineSecrets.RecoveryNotes(codes, NotesPassword))));
        var prompts = new TestPrompts(ExportPassword);
        var pipeline = new VaultPipeline(prompts.Environment());
        var scanned = VaultPipeline.Scan(pipeline.Load([keeper]));
        var proposed = pipeline.Merge(scanned, new MergeRequest());
        var findings = proposed.Findings.ShouldHaveSingleItem();
        var passwordFinding = findings.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Password);
        var decisions = PipelineDecisions.Findings(findings.OriginKey, new FindingChoice(passwordFinding.Id, FindingDecision.Accepted));
        File.Delete(keeper);

        var decided = pipeline.Merge(scanned, new MergeRequest { Decisions = decisions });

        proposed.Merged.Items.ShouldHaveSingleItem().Login.ShouldNotBeNull().Password.ShouldBeNull();
        decided.Merged.Items.ShouldHaveSingleItem().Login.ShouldNotBeNull().Password.ShouldBe(NotesPassword);
        decided.Findings.ShouldHaveSingleItem().AcceptedFindingIds.Count.ShouldBe(2);
        prompts.FilePasswordPrompts.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_checks_the_output_before_merging_a_scanned_vault()
    {
        string json = BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", NewPassword, "https://example.com"));
        string input = Write(directory, "bitwarden.json", json);
        var prompts = new TestPrompts(ExportPassword);
        var pipeline = new VaultPipeline(prompts.Environment());
        var scanned = VaultPipeline.Scan(pipeline.Load([input]));

        var overInput = () => pipeline.Merge(scanned, new MergeRequest { Output = new PipelineOutput(input, Plaintext: true, Force: true) });
        var missingDirectory = () => pipeline.Merge(scanned, new MergeRequest { Output = new PipelineOutput(Path.Combine(directory, "missing", "merged.json")) });

        overInput.ShouldThrow<IOException>().Message.ShouldContain("also an input", StringComparison.Ordinal);
        missingDirectory.ShouldThrow<IOException>().Message.ShouldContain("does not exist", StringComparison.Ordinal);
        File.ReadAllText(input).ShouldBe(json);
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_warns_when_an_input_archive_can_no_longer_be_opened()
    {
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(510);
        string zip = Write(directory, "keeper.zip", CommanderZip(
            KeeperJson(KeeperFileRecord("rec-files", "GitHub backup", "file-codes", "github-recovery-codes.txt")),
            ("files/file-codes", string.Join('\n', codes))));
        var loaded = new VaultPipeline(new TestPrompts(ExportPassword).Environment()).Load([zip]);
        File.Delete(zip);

        var scanned = VaultPipeline.Scan(loaded);

        scanned.Items.ShouldHaveSingleItem().Findings.ShouldBeEmpty();
        scanned.Warnings.ShouldHaveSingleItem().ShouldStartWith("keeper.zip: attachments were not scanned", StringComparison.Ordinal);
    }

    [Fact]
    public void Load_needs_at_least_one_input()
    {
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var action = () => pipeline.Load([]);

        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Run_names_items_created_by_placement_after_their_source_and_finding_and_merges_them()
    {
        string notes = "deploy key\n" + PemFixtures.Ed25519OpenSsh();
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperNote("rec-server", "Server", notes)));
        string bitwarden = Write(directory, "bitwarden.json", BitwardenJson(BitwardenNote("bw-server", "Server", notes)));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var first = pipeline.Run(new PipelineRequest([keeper, bitwarden]));
        var second = pipeline.Run(new PipelineRequest([keeper, bitwarden]));

        first.Findings.Count.ShouldBe(2);
        string keeperFinding = first.Findings[0].Findings.ShouldHaveSingleItem().Id;
        string bitwardenFinding = first.Findings[1].Findings.ShouldHaveSingleItem().Id;
        var keyGroup = first.Match.Groups.ShouldHaveSingleItem(group => group.Members[0].Type == VaultItemType.SshKey);
        keyGroup.MemberKeys.ShouldBe(["keeper|keeper.json|rec-server/" + keeperFinding, "bitwarden|bitwarden.json|bw-server/" + bitwardenFinding]);
        keyGroup.Members[0].Origin.OriginalId.ShouldBe("rec-server/" + keeperFinding);
        keyGroup.Members[1].RevisionDate.ShouldBe(BitwardenRevision);
        second.Match.Groups.Select(group => group.Key).ShouldBe(first.Match.Groups.Select(group => group.Key));
        var key = first.Merged.Items.ShouldHaveSingleItem(item => item.Type == VaultItemType.SshKey).SshKey.ShouldNotBeNull();
        key.KeyFingerprint.ShouldBe(PemFixtures.Ed25519Fingerprint);
        first.Merged.Items.ShouldHaveSingleItem(item => item.Type == VaultItemType.SecureNote).Notes.ShouldBe("deploy key");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_writes_each_card_found_in_one_note_as_its_own_item(bool preferKeeper)
    {
        string visa = TestSecrets.CardNumber("4", 16);
        string mastercard = TestSecrets.CardNumber("51", 16);
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperNote("rec-cards", "Bank cards", "Visa " + visa + "\nMastercard " + mastercard)));
        string output = Path.Combine(directory, "merged.json");
        var policy = preferKeeper ? new MergePolicy(MergePolicyKind.PreferSource, "keeper.json") : new MergePolicy();
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([keeper]) { Policy = policy, Output = new PipelineOutput(output, Plaintext: true) });

        result.HasUnresolvedConflicts.ShouldBeFalse();
        result.OutputWritten.ShouldBeTrue();
        VaultItem[] cards = [.. BitwardenJsonReader.Read(File.ReadAllText(output), "merged.json").Items.Where(item => item.Type == VaultItemType.Card)];
        cards.Select(card => card.Name).ShouldBe(["Bank cards", "Bank cards (2)"]);
        cards.Select(card => card.Card?.Number).ShouldBe([visa, mastercard]);
        cards.ShouldAllSatisfy(card => card.Fields.ShouldBeEmpty());
    }

    [Fact]
    public void Run_merges_items_created_from_the_same_note_in_both_inputs_by_their_numbered_names()
    {
        string visa = TestSecrets.CardNumber("4", 16);
        string mastercard = TestSecrets.CardNumber("51", 16);
        string notes = "Visa " + visa + "\nMastercard " + mastercard;
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperNote("rec-cards", "Bank cards", notes)));
        string bitwarden = Write(directory, "bitwarden.json", BitwardenJson(BitwardenNote("bw-cards", "Bank cards", notes)));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([keeper, bitwarden]));

        result.HasUnresolvedConflicts.ShouldBeFalse();
        result.Match.Groups.Select(group => group.Members.Count).ShouldBe([2, 2, 2]);
        VaultItem[] cards = [.. result.Merged.Items.Where(item => item.Type == VaultItemType.Card)];
        cards.Select(card => card.Name).ShouldBe(["Bank cards", "Bank cards (2)"]);
        cards.Select(card => card.Card?.Number).ShouldBe([visa, mastercard]);
        cards.ShouldAllSatisfy(card => card.Fields.ShouldBeEmpty());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_writes_each_ssh_key_found_in_one_note_as_its_own_item(bool preferKeeper)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string ecdsaKey = PemFixtures.Pkcs8(ecdsa);
        string notes = "web\n" + PemFixtures.Ed25519OpenSsh() + "\nbuild\n" + ecdsaKey;
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperNote("rec-servers", "Servers", notes)));
        string output = Path.Combine(directory, "merged.json");
        var policy = preferKeeper ? new MergePolicy(MergePolicyKind.PreferSource, "keeper.json") : new MergePolicy();
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([keeper]) { Policy = policy, Output = new PipelineOutput(output, Plaintext: true) });

        result.HasUnresolvedConflicts.ShouldBeFalse();
        result.OutputWritten.ShouldBeTrue();
        VaultItem[] keys = [.. BitwardenJsonReader.Read(File.ReadAllText(output), "merged.json").Items.Where(item => item.Type == VaultItemType.SshKey)];
        keys.Select(key => key.Name).ShouldBe(["Servers SSH key", "Servers SSH key (2)"]);
        keys.Select(key => key.SshKey?.PrivateKey).ShouldBe([PemFixtures.Ed25519OpenSsh(), ecdsaKey]);
        keys.ShouldAllSatisfy(key => key.Fields.ShouldBeEmpty());
    }

    [Fact]
    public void Run_writes_a_second_key_stored_with_an_ssh_key_as_its_own_item()
    {
        using var first = RSA.Create(2048);
        using var second = RSA.Create(2048);
        string privateKeys = PemFixtures.RsaPkcs1(first) + "\n" + PemFixtures.RsaPkcs1(second);
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperSshKey("rec-ssh", "Deploy keys", privateKeys, string.Empty)));
        string output = Path.Combine(directory, "merged.json");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var result = pipeline.Run(new PipelineRequest([keeper]) { Output = new PipelineOutput(output, Plaintext: true) });

        result.HasUnresolvedConflicts.ShouldBeFalse();
        result.OutputWritten.ShouldBeTrue();
        var items = BitwardenJsonReader.Read(File.ReadAllText(output), "merged.json").Items;
        items.Select(item => item.Name).ShouldBe(["Deploy keys", "Deploy keys SSH key"]);
        items.Select(item => item.SshKey?.PrivateKey).ShouldBe([PemFixtures.Pkcs8(first), PemFixtures.Pkcs8(second)]);
        items.ShouldAllSatisfy(item => item.Fields.ShouldBeEmpty());
    }

    [Fact]
    public void Run_keeps_the_origin_key_of_an_item_changed_by_an_accepted_finding()
    {
        string csv = Write(
            directory,
            "bitwarden.csv",
            "folder,favorite,type,name,notes,fields,reprompt,login_uri,login_username,login_password,login_totp\n"
            + ",,login,Example,Username: alice,,0,https://example.com,,fixture-csv-password,\n");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());
        var findings = pipeline.Run(new PipelineRequest([csv])).Findings.ShouldHaveSingleItem();
        var username = findings.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Username);
        var decisions = PipelineDecisions.Findings(findings.OriginKey, new FindingChoice(username.Id, FindingDecision.Accepted));

        var result = pipeline.Run(new PipelineRequest([csv]) { Decisions = decisions });

        result.Merged.Items.ShouldHaveSingleItem().Login.ShouldNotBeNull().Username.ShouldBe("alice");
        findings.OriginKey.ShouldStartWith("bitwarden|bitwarden.csv|content:", StringComparison.Ordinal);
        result.Findings.ShouldHaveSingleItem().OriginKey.ShouldBe(findings.OriginKey);
        result.Match.Groups.ShouldHaveSingleItem().MemberKeys.ShouldBe([findings.OriginKey]);
    }
}
