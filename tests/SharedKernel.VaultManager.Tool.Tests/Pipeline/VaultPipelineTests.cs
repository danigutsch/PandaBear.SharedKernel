using System.Security.Cryptography;
using System.Text.Json;
using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Secrets;
using PandaBear.VaultManager.Tool.Tests.Secrets;
using static PandaBear.VaultManager.Tool.Tests.Pipeline.PipelineInputs;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

/// <summary>Tests the vault pipeline: loading, scanning, merging, secret placement, attachment listing, and writing the merged export.</summary>
public sealed class VaultPipelineTests : IDisposable
{
    private const string OldPassword = "fixture-old-keeper-password";
    private const string NewPassword = "fixture-new-bitwarden-password";
    private const string NotesPassword = "Gh-notes-Passw0rd!";
    private const string ExportPassword = "fixture export password";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-pipeline-tests-").FullName;

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    /// <summary>The merged vault keeps the folders of every input, including empty ones.</summary>
    [Fact]
    public void Run_keeps_the_folders_of_every_input_in_the_merged_vault()
    {
        // Arrange
        string keeper = Write(directory, "keeper.json", """{"shared_folders":[{"uid":"s1","path":"Team\\Empty"}],"records":[]}""");
        string bitwarden = Write(directory, "bitwarden.json", """{"encrypted":false,"folders":[{"id":"f","name":"Archive"}],"items":[]}""");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper, bitwarden]));

        // Assert
        result.Merged.Folders.ShouldBe(["Team/Empty", "Archive"]);
    }

    /// <summary>A run keeps the newer Bitwarden password and the Keeper TOTP, moves the old password into history, and writes an encrypted export.</summary>
    [Fact]
    public void Run_keeps_the_newer_bitwarden_password_the_keeper_totp_and_the_old_password_in_history()
    {
        // Arrange
        string totp = PipelineSecrets.TotpUri(PipelineSecrets.TotpSecret(300));
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperLogin("rec-github", "GitHub", "alice", OldPassword, "https://github.com/login", totp: totp)));
        string bitwarden = Write(directory, "bitwarden.json", BitwardenJson(BitwardenLogin("bw-github", "GitHub", "alice", NewPassword, "https://github.com")));
        string output = Path.Combine(directory, "merged.json");
        var prompts = new TestPrompts(ExportPassword);
        var pipeline = new VaultPipeline(prompts.Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper, bitwarden]) { Output = new PipelineOutput(output) });

        // Assert
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

    /// <summary>A run places pre-accepted recovery codes in a hidden field and leaves the proposed notes password in the notes.</summary>
    [Fact]
    public void Run_places_pre_accepted_recovery_codes_and_leaves_the_proposed_notes_password_in_notes()
    {
        // Arrange
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(400);
        string keeper = Write(directory, "keeper.json", KeeperJson(
            KeeperLogin("rec-github", "GitHub", "alice", url: "https://github.com", notes: PipelineSecrets.RecoveryNotes(codes, NotesPassword))));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper]));

        // Assert
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

    /// <summary>Accepting decisions moves recovery codes and a notes password into their fields and leaves the notes clean.</summary>
    [Fact]
    public void Run_moves_recovery_codes_and_an_accepted_notes_password_into_their_fields()
    {
        // Arrange
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(410);
        string keeper = Write(directory, "keeper.json", KeeperJson(
            KeeperLogin("rec-github", "GitHub", "alice", url: "https://github.com", notes: PipelineSecrets.RecoveryNotes(codes, NotesPassword))));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());
        var findings = pipeline.Run(new PipelineRequest([keeper])).Findings.ShouldHaveSingleItem();
        var passwordFinding = findings.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Password);
        var decisions = PipelineDecisions.Findings(findings.OriginKey, new FindingChoice(passwordFinding.Id, FindingDecision.Accepted));

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper]) { Decisions = decisions });

        // Assert
        var item = result.Merged.Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Password.ShouldBe(NotesPassword);
        var field = item.Fields.ShouldHaveSingleItem();
        field.Name.ShouldBe("Recovery codes");
        field.Type.ShouldBe(CustomFieldType.Hidden);
        field.Value.ShouldBe(string.Join('\n', codes));
        item.Notes.ShouldBe("GitHub recovery codes");
        result.PlacementProblems.ShouldBeEmpty();
    }

    /// <summary>A finding rejected in the decisions is left in the notes, and a proposed finding accepted there is applied.</summary>
    [Fact]
    public void Run_skips_a_finding_rejected_in_decisions_and_applies_a_proposed_one_accepted_there()
    {
        // Arrange
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

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper]) { Decisions = decisions });

        // Assert
        result.Findings.ShouldHaveSingleItem().AcceptedFindingIds.ShouldBe([passwordFinding.Id]);
        var item = result.Merged.Items.ShouldHaveSingleItem();
        item.Fields.ShouldBeEmpty();
        item.Login.ShouldNotBeNull().Password.ShouldBe(NotesPassword);
        string notes = item.Notes.ShouldNotBeNull();
        codes.ShouldAllSatisfy(code => notes.ShouldContain(code, StringComparison.Ordinal));
        notes.ShouldNotContain(NotesPassword, StringComparison.Ordinal);
    }

    /// <summary>Items found in only one input pass through unchanged and count as unique groups.</summary>
    [Fact]
    public void Run_passes_through_items_found_in_only_one_input()
    {
        // Arrange
        string keeper = Write(directory, "keeper.json", KeeperJson(
            KeeperLogin("rec-keeper", "Keeper only", "carol", "fixture-keeper-only-password", "https://keeper-only.example.com")));
        string bitwarden = Write(directory, "bitwarden.json", BitwardenJson(
            BitwardenLogin("bw-only", "Bitwarden only", "dave", "fixture-bitwarden-only-password", "https://bitwarden-only.example.com")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper, bitwarden]));

        // Assert
        result.Merged.Items.Select(item => item.Name).ShouldBe(["Keeper only", "Bitwarden only"]);
        result.Merged.Items[0].Login.ShouldNotBeNull().Password.ShouldBe("fixture-keeper-only-password");
        result.Merged.Items[1].Login.ShouldNotBeNull().Password.ShouldBe("fixture-bitwarden-only-password");
        result.MergeReport.UniqueGroups.ShouldBe(2);
        result.HasUnresolvedConflicts.ShouldBeFalse();
    }

    /// <summary>A Keeper SSH key without a fingerprint is completed with the fingerprint and public key.</summary>
    [Fact]
    public void Run_completes_a_keeper_ssh_key_that_has_no_fingerprint()
    {
        // Arrange
        string publicKey = PemFixtures.Ed25519PublicKey + " deploy@example";
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperSshKey("rec-ssh", "Deploy key", PemFixtures.Ed25519OpenSsh(), publicKey)));
        string output = Path.Combine(directory, "merged.json");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper]) { Output = new PipelineOutput(output, Plaintext: true) });

        // Assert
        result.OutputWritten.ShouldBeTrue();
        var item = BitwardenJsonReader.Read(File.ReadAllText(output), "merged.json").Items.ShouldHaveSingleItem();
        item.Type.ShouldBe(VaultItemType.SshKey);
        var key = item.SshKey.ShouldNotBeNull();
        key.KeyFingerprint.ShouldBe(PemFixtures.Ed25519Fingerprint);
        key.PublicKey.ShouldBe(publicKey);
        key.PrivateKey.ShouldBe(PemFixtures.Ed25519OpenSsh());
    }

    /// <summary>A Keeper SSH key whose body is not Base64 is written as a secure note that keeps the key in a hidden field.</summary>
    [Fact]
    public void Run_writes_a_keeper_ssh_key_whose_body_is_not_base64_as_a_secure_note_that_keeps_the_key()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        string privateKey = PemFixtures.WithBrokenBody(PemFixtures.RsaPkcs1(rsa), shortenLine: false);
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperSshKey("rec-ssh", "Deploy key", privateKey, string.Empty)));
        string output = Path.Combine(directory, "merged.json");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());
        var proposed = pipeline.Run(new PipelineRequest([keeper])).PendingFindings.ShouldHaveSingleItem();
        var rejected = PipelineDecisions.Findings(proposed.OriginKey, [.. proposed.Choices.Select(choice => choice with { Decision = FindingDecision.Rejected })]);

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper]) { Decisions = rejected, Output = new PipelineOutput(output, Plaintext: true) });

        // Assert
        result.OutputWritten.ShouldBeTrue();
        var item = BitwardenJsonReader.Read(File.ReadAllText(output), "merged.json").Items.ShouldHaveSingleItem();
        item.Name.ShouldBe("Deploy key");
        item.Type.ShouldBe(VaultItemType.SecureNote);
        item.Fields.ShouldBe([new CustomField(SshKeyNormalizer.PrivateKeyField, privateKey, CustomFieldType.Hidden)]);
    }

    /// <summary>A Keeper SSH key with explicit curve parameters is written as a secure note that keeps the key and reports no finding.</summary>
    [Fact]
    public void Run_writes_a_keeper_ssh_key_with_explicit_curve_parameters_as_a_secure_note_that_keeps_the_key()
    {
        // Arrange
        using var named = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var explicitCurve = ECDsa.Create(named.ExportExplicitParameters(includePrivateParameters: true));
        string privateKey = PemFixtures.Sec1(explicitCurve);
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperSshKey("rec-ssh", "Deploy key", privateKey, string.Empty)));
        string output = Path.Combine(directory, "merged.json");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper]) { Output = new PipelineOutput(output, Plaintext: true) });

        // Assert
        result.OutputWritten.ShouldBeTrue();
        result.Findings.ShouldBeEmpty();
        result.PlacementProblems.ShouldBeEmpty();
        var item = BitwardenJsonReader.Read(File.ReadAllText(output), "merged.json").Items.ShouldHaveSingleItem();
        item.Type.ShouldBe(VaultItemType.SecureNote);
        item.Fields.ShouldBe([new CustomField(SshKeyNormalizer.PrivateKeyField, privateKey, CustomFieldType.Hidden)]);
    }

    /// <summary>Text stored around a Keeper SSH key is written to a hidden field of the key item.</summary>
    [Fact]
    public void Run_writes_text_stored_around_a_keeper_ssh_key_to_a_hidden_field_of_the_key_item()
    {
        // Arrange
        const string Label = "deploy key for git.example.com";
        const string HostLine = "host: prod-db-1";
        string stored = Label + "\n" + PemFixtures.Ed25519OpenSsh() + "\n" + HostLine;
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperSshKey("rec-ssh", "Deploy key", stored, string.Empty)));
        string output = Path.Combine(directory, "merged.json");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper]) { Output = new PipelineOutput(output, Plaintext: true) });

        // Assert
        result.OutputWritten.ShouldBeTrue();
        var item = BitwardenJsonReader.Read(File.ReadAllText(output), "merged.json").Items.ShouldHaveSingleItem();
        item.SshKey.ShouldNotBeNull().PrivateKey.ShouldBe(PemFixtures.Ed25519OpenSsh());
        item.Fields.ShouldBe([new CustomField(SshKeyNormalizer.OtherTextField, Label + "\n" + HostLine, CustomFieldType.Hidden)]);
    }

    /// <summary>Recovery codes in a Commander zip attachment are scanned as a finding and the file is listed for manual upload.</summary>
    [Fact]
    public void Run_scans_recovery_codes_in_a_commander_zip_attachment_and_lists_the_file_for_upload()
    {
        // Arrange
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(500);
        string zip = Write(directory, "keeper.zip", CommanderZip(
            KeeperJson(KeeperFileRecord("rec-files", "GitHub backup", "file-codes", "github-recovery-codes.txt")),
            ("files/file-codes", string.Join('\n', codes) + "\n")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([zip]));

        // Assert
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

    /// <summary>Attachments missing from the export are listed for download from the source vault.</summary>
    [Fact]
    public void Run_lists_attachments_missing_from_the_export_for_download_from_the_source_vault()
    {
        // Arrange
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperFileRecord("rec-files", "GitHub backup", "file-codes", "github-recovery-codes.txt")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper]));

        // Assert
        result.Findings.ShouldBeEmpty();
        result.ManualAttachments.ShouldBe([new ManualAttachment("GitHub backup", "github-recovery-codes.txt", "keeper.json", null)]);
    }

    /// <summary>Two attachments with the same file name on one Keeper item are listed separately.</summary>
    [Fact]
    public void Run_lists_two_attachments_with_one_file_name_on_one_keeper_item_separately()
    {
        // Arrange
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperFileRecord("rec-files", "Scans", ("file-one", "scan.pdf"), ("file-two", "scan.pdf"))));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper]));

        // Assert
        result.ManualAttachments.ShouldBe(
        [
            new ManualAttachment("Scans", "scan.pdf", "keeper.json", null),
            new ManualAttachment("Scans", "scan.pdf", "keeper.json", null),
        ]);
    }

    /// <summary>Two files with the same name and size on one Commander item are listed separately.</summary>
    [Fact]
    public void Run_lists_two_files_with_one_name_and_size_on_one_commander_item_separately()
    {
        // Arrange
        string zip = Write(directory, "keeper.zip", CommanderZip(
            KeeperJson(KeeperFileRecord("rec-files", "Scans", ("file-one", "scan.txt"), ("file-two", "scan.txt"))),
            ("files/file-one", "first scan text\n"),
            ("files/file-two", "other scan text\n")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([zip]));

        // Assert
        result.ManualAttachments.ShouldBe(
        [
            new ManualAttachment("Scans", "scan.txt", "keeper.zip", "files/file-one"),
            new ManualAttachment("Scans", "scan.txt", "keeper.zip", "files/file-two"),
        ]);
    }

    /// <summary>A file found on two merged items is listed once per item file.</summary>
    [Fact]
    public void Run_lists_a_file_found_on_two_merged_items_once()
    {
        // Arrange
        string zip = Write(directory, "keeper.zip", CommanderZip(
            KeeperJson(
                WithAttachments(KeeperLogin("rec-one", "GitHub", "alice", OldPassword, "https://github.com"), ("file-a", "codes.txt"), ("file-b", "codes.txt")),
                WithAttachments(KeeperLogin("rec-two", "GitHub", "alice", OldPassword, "https://github.com"), ("file-c", "codes.txt"))),
            ("files/file-a", "first codes\n"),
            ("files/file-b", "other codes\n"),
            ("files/file-c", "first codes\n")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([zip]));

        // Assert
        result.Match.Groups.ShouldHaveSingleItem().Members.Count.ShouldBe(2);
        result.ManualAttachments.ShouldBe(
        [
            new ManualAttachment("GitHub", "codes.txt", "keeper.zip", "files/file-a"),
            new ManualAttachment("GitHub", "codes.txt", "keeper.zip", "files/file-b"),
        ]);
    }

    /// <summary>Files with the same name and size but different content on two merged items are listed separately.</summary>
    [Fact]
    public void Run_lists_files_with_one_name_and_size_but_different_content_on_two_merged_items_separately()
    {
        // Arrange
        var older = WithAttachments(KeeperLogin("rec-old", "GitHub", "alice", OldPassword, "https://github.com"), ("file-old", "github-recovery-codes.txt"));
        var newer = WithAttachments(KeeperLogin("rec-new", "GitHub", "alice", OldPassword, "https://github.com"), ("file-new", "github-recovery-codes.txt"));
        newer["last_modified"] = KeeperModified.AddDays(30).ToUnixTimeSeconds();
        string zip = Write(directory, "keeper.zip", CommanderZip(
            KeeperJson(older, newer),
            ("files/file-old", "old1-aaaa old2-bbbb\n"),
            ("files/file-new", "new1-cccc new2-dddd\n")));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([zip]));

        // Assert
        result.Match.Groups.ShouldHaveSingleItem().Members.Count.ShouldBe(2);
        result.ManualAttachments.ShouldBe(
        [
            new ManualAttachment("GitHub", "github-recovery-codes.txt", "keeper.zip", "files/file-new"),
            new ManualAttachment("GitHub", "github-recovery-codes.txt", "keeper.zip", "files/file-old"),
        ]);
    }

    /// <summary>A file found in two exports is listed once, from the newest item.</summary>
    [Fact]
    public void Run_lists_a_file_found_in_two_exports_once_from_the_newest_item()
    {
        // Arrange
        byte[] scan = [.. "%PDF-1.7\n%synthetic scan\n"u8];
        string keeper = Write(directory, "keeper.zip", TestZip.Create(
            ("export.json", TestZip.Utf8(KeeperJson(WithAttachments(KeeperLogin("rec-github", "GitHub", "alice", OldPassword, "https://github.com"), ("file-scan", "scan.pdf"))))),
            ("files/file-scan", scan)));
        string bitwarden = Write(directory, "bitwarden.zip", TestZip.Create(
            ("data.json", TestZip.Utf8(BitwardenJson(BitwardenLogin("bw-github", "GitHub", "alice", NewPassword, "https://github.com")))),
            ("attachments/GitHub/scan.pdf", scan)));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper, bitwarden]));

        // Assert
        result.Match.Groups.ShouldHaveSingleItem().Members.Count.ShouldBe(2);
        result.ManualAttachments.ShouldBe([new ManualAttachment("GitHub", "scan.pdf", "bitwarden.zip", "attachments/GitHub/scan.pdf")]);
    }

    /// <summary>Same-named attachments missing from the export on two merged items are listed separately.</summary>
    [Fact]
    public void Run_lists_same_named_attachments_missing_from_the_export_on_two_merged_items_separately()
    {
        // Arrange
        string keeper = Write(directory, "keeper.json", KeeperJson(
            WithAttachments(KeeperLogin("rec-one", "GitHub", "alice", OldPassword, "https://github.com"), ("file-one", "scan.pdf")),
            WithAttachments(KeeperLogin("rec-two", "GitHub", "alice", OldPassword, "https://github.com"), ("file-two", "scan.pdf"))));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper]));

        // Assert
        result.Match.Groups.ShouldHaveSingleItem().Members.Count.ShouldBe(2);
        result.ManualAttachments.ShouldBe(
        [
            new ManualAttachment("GitHub", "scan.pdf", "keeper.json", null),
            new ManualAttachment("GitHub", "scan.pdf", "keeper.json", null),
        ]);
    }

    /// <summary>Origin keys stay unique when an input ID equals the key of an item created by placement.</summary>
    [Fact]
    public void Run_keeps_origin_keys_unique_when_an_input_ID_equals_the_key_of_a_created_item()
    {
        // Arrange
        string notes = "deploy key\n" + PemFixtures.Ed25519OpenSsh();
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperNote("rec-server", "Server", notes)));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());
        string findingId = pipeline.Run(new PipelineRequest([keeper])).Findings.ShouldHaveSingleItem().Findings.ShouldHaveSingleItem().Id;
        string createdId = "rec-server/" + findingId;
        Write(directory, "keeper.json", KeeperJson(KeeperNote("rec-server", "Server", notes), KeeperNote(createdId, "Other", "plain note")));

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper]));

        // Assert
        string[] keys = [.. result.Match.Groups.SelectMany(group => group.MemberKeys)];
        keys.ShouldBe(["keeper|keeper.json|rec-server", "keeper|keeper.json|" + createdId + "#2", "keeper|keeper.json|" + createdId]);
        var created = result.Merged.Items.ShouldHaveSingleItem(item => item.Type == VaultItemType.SshKey);
        created.Origin.OriginalId.ShouldBe(createdId + "#2");
    }

    /// <summary>Merge applies new decisions to a scanned vault without reading the inputs or asking for passwords again.</summary>
    [Fact]
    public void Merge_applies_new_decisions_to_a_scanned_vault_without_reading_the_inputs_again()
    {
        // Arrange
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(800);
        string keeper = Write(directory, "keeper.json", KeeperJson(
            KeeperLogin("rec-github", "GitHub", "alice", url: "https://github.com", notes: PipelineSecrets.RecoveryNotes(codes, NotesPassword))));
        var prompts = new TestPrompts(ExportPassword);
        var pipeline = new VaultPipeline(prompts.Environment());
        var scanned = VaultPipeline.Scan(pipeline.Load([keeper], TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        var proposed = pipeline.Merge(scanned, new MergeRequest(), TestContext.Current.CancellationToken);
        var findings = proposed.Findings.ShouldHaveSingleItem();
        var passwordFinding = findings.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Password);
        var decisions = PipelineDecisions.Findings(findings.OriginKey, new FindingChoice(passwordFinding.Id, FindingDecision.Accepted));
        File.Delete(keeper);

        // Act
        var decided = pipeline.Merge(scanned, new MergeRequest { Decisions = decisions }, TestContext.Current.CancellationToken);

        // Assert
        proposed.Merged.Items.ShouldHaveSingleItem().Login.ShouldNotBeNull().Password.ShouldBeNull();
        decided.Merged.Items.ShouldHaveSingleItem().Login.ShouldNotBeNull().Password.ShouldBe(NotesPassword);
        decided.Findings.ShouldHaveSingleItem().AcceptedFindingIds.Count.ShouldBe(2);
        prompts.FilePasswordPrompts.ShouldBeEmpty();
    }

    /// <summary>Merge checks the output path before merging and refuses an input file or a missing directory.</summary>
    [Fact]
    public void Merge_checks_the_output_before_merging_a_scanned_vault()
    {
        // Arrange
        string json = BitwardenJson(BitwardenLogin("bw-1", "Example", "alice", NewPassword, "https://example.com"));
        string input = Write(directory, "bitwarden.json", json);
        var prompts = new TestPrompts(ExportPassword);
        var pipeline = new VaultPipeline(prompts.Environment());
        var scanned = VaultPipeline.Scan(pipeline.Load([input], TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        var overInput = () => pipeline.Merge(scanned, new MergeRequest { Output = new PipelineOutput(input, Plaintext: true, Force: true) }, TestContext.Current.CancellationToken);
        var missingDirectory = () => pipeline.Merge(scanned, new MergeRequest { Output = new PipelineOutput(Path.Combine(directory, "missing", "merged.json")) }, TestContext.Current.CancellationToken);

        // Act
        var overInputException = overInput.ShouldThrow<IOException>();
        var missingDirectoryException = missingDirectory.ShouldThrow<IOException>();

        // Assert
        overInputException.Message.ShouldContain("also an input", StringComparison.Ordinal);
        missingDirectoryException.Message.ShouldContain("does not exist", StringComparison.Ordinal);
        File.ReadAllText(input).ShouldBe(json);
        prompts.NewPasswordPrompts.ShouldBeEmpty();
    }

    /// <summary>Scan warns when an input archive can no longer be opened and reports no findings for its items.</summary>
    [Fact]
    public void Scan_warns_when_an_input_archive_can_no_longer_be_opened()
    {
        // Arrange
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(510);
        string zip = Write(directory, "keeper.zip", CommanderZip(
            KeeperJson(KeeperFileRecord("rec-files", "GitHub backup", "file-codes", "github-recovery-codes.txt")),
            ("files/file-codes", string.Join('\n', codes))));
        var loaded = new VaultPipeline(new TestPrompts(ExportPassword).Environment()).Load([zip], TestContext.Current.CancellationToken);
        File.Delete(zip);

        // Act
        var scanned = VaultPipeline.Scan(loaded, TestContext.Current.CancellationToken);

        // Assert
        scanned.Items.ShouldHaveSingleItem().Findings.ShouldBeEmpty();
        scanned.Warnings.ShouldHaveSingleItem().ShouldStartWith("keeper.zip: attachments were not scanned", StringComparison.Ordinal);
    }

    /// <summary>Load and run both keep the files of attachment folders that two items named Bank share, without listing them as manual attachments.</summary>
    [Fact]
    public void Load_and_run_keep_the_files_of_attachment_folders_that_two_items_named_bank_share()
    {
        // Arrange
        string zip = Write(directory, "bitwarden.zip", TwoBanksZip());
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var loaded = pipeline.Load([zip], TestContext.Current.CancellationToken);
        var result = pipeline.Run(new PipelineRequest([zip]));

        // Assert
        IReadOnlyList<UnassignedAttachmentFolder>[] lists = [loaded.UnassignedAttachmentFolders, result.UnassignedAttachmentFolders];
        lists.ShouldAllSatisfy(folders =>
        {
            folders.Select(folder => (folder.SourceFile, folder.FolderName, folder.MatchingItemCount)).ShouldBe([("bitwarden.zip", "Bank", 2), ("bitwarden.zip", "Bank_1", 2)]);
            folders.SelectMany(folder => folder.Files).Select(file => (file.FileName, file.SourcePath)).ShouldBe(
            [
                ("statement.txt", "attachments/Bank/statement.txt"),
                ("contract.txt", "attachments/Bank_1/contract.txt"),
            ]);
        });
        result.ManualAttachments.ShouldBeEmpty();
    }

    /// <summary>Load names an input with malformed content and keeps the reader error inside.</summary>
    [Fact]
    public void Load_names_an_input_whose_content_is_malformed_and_keeps_the_reader_error_inside()
    {
        // Arrange
        const string Value = "fixture-unquoted-secret";
        string keeper = Write(directory, "keeper.json", "[{\"title\": \"Example\", \"password\": " + Value + "}]");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var action = () => pipeline.Load([keeper], TestContext.Current.CancellationToken);

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldBe("keeper.json: Keeper export is not valid JSON.");
        var readerError = exception.InnerException.ShouldBeOfType<InvalidDataException>();
        readerError.Message.ShouldBe("Keeper export is not valid JSON.");
        readerError.InnerException.ShouldBeAssignableTo<JsonException>();
    }

    /// <summary>Load names a Bitwarden input that holds a null item.</summary>
    [Fact]
    public void Load_names_a_bitwarden_input_that_holds_a_null_item()
    {
        // Arrange
        string input = Write(directory, "bitwarden.json", """{"encrypted":false,"folders":[],"items":[null]}""");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var action = () => pipeline.Load([input], TestContext.Current.CancellationToken);

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldBe("bitwarden.json: Item 1 of the Bitwarden export is null.");
    }

    /// <summary>Load throws when given no inputs.</summary>
    [Fact]
    public void Load_needs_at_least_one_input()
    {
        // Arrange
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        var action = () => pipeline.Load([], TestContext.Current.CancellationToken);

        // Act, Assert
        action.ShouldThrow<ArgumentException>();
    }

    /// <summary>Items created by placement are named after their source and finding, stay stable across runs, and merge across inputs.</summary>
    [Fact]
    public void Run_names_items_created_by_placement_after_their_source_and_finding_and_merges_them()
    {
        // Arrange
        string notes = "deploy key\n" + PemFixtures.Ed25519OpenSsh();
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperNote("rec-server", "Server", notes)));
        string bitwarden = Write(directory, "bitwarden.json", BitwardenJson(BitwardenNote("bw-server", "Server", notes)));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var first = pipeline.Run(new PipelineRequest([keeper, bitwarden]));
        var second = pipeline.Run(new PipelineRequest([keeper, bitwarden]));

        // Assert
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

    /// <summary>Each card found in one note is written as its own item, whichever source the policy prefers.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_writes_each_card_found_in_one_note_as_its_own_item(bool preferKeeper)
    {
        // Arrange
        string visa = TestSecrets.CardNumber("4", 16);
        string mastercard = TestSecrets.CardNumber("51", 16);
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperNote("rec-cards", "Bank cards", "Visa " + visa + "\nMastercard " + mastercard)));
        string output = Path.Combine(directory, "merged.json");
        var policy = preferKeeper ? new MergePolicy(MergePolicyKind.PreferSource, "keeper.json") : new MergePolicy();
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper]) { Policy = policy, Output = new PipelineOutput(output, Plaintext: true) });

        // Assert
        result.HasUnresolvedConflicts.ShouldBeFalse();
        result.OutputWritten.ShouldBeTrue();
        VaultItem[] cards = [.. BitwardenJsonReader.Read(File.ReadAllText(output), "merged.json").Items.Where(item => item.Type == VaultItemType.Card)];
        cards.Select(card => card.Name).ShouldBe(["Bank cards", "Bank cards (2)"]);
        cards.Select(card => card.Card?.Number).ShouldBe([visa, mastercard]);
        cards.ShouldAllSatisfy(card => card.Fields.ShouldBeEmpty());
    }

    /// <summary>Items created from the same note in both inputs merge by their numbered names.</summary>
    [Fact]
    public void Run_merges_items_created_from_the_same_note_in_both_inputs_by_their_numbered_names()
    {
        // Arrange
        string visa = TestSecrets.CardNumber("4", 16);
        string mastercard = TestSecrets.CardNumber("51", 16);
        string notes = "Visa " + visa + "\nMastercard " + mastercard;
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperNote("rec-cards", "Bank cards", notes)));
        string bitwarden = Write(directory, "bitwarden.json", BitwardenJson(BitwardenNote("bw-cards", "Bank cards", notes)));
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper, bitwarden]));

        // Assert
        result.HasUnresolvedConflicts.ShouldBeFalse();
        result.Match.Groups.Select(group => group.Members.Count).ShouldBe([2, 2, 2]);
        VaultItem[] cards = [.. result.Merged.Items.Where(item => item.Type == VaultItemType.Card)];
        cards.Select(card => card.Name).ShouldBe(["Bank cards", "Bank cards (2)"]);
        cards.Select(card => card.Card?.Number).ShouldBe([visa, mastercard]);
        cards.ShouldAllSatisfy(card => card.Fields.ShouldBeEmpty());
    }

    /// <summary>Each SSH key found in one note is written as its own item, whichever source the policy prefers.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_writes_each_ssh_key_found_in_one_note_as_its_own_item(bool preferKeeper)
    {
        // Arrange
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string ecdsaKey = PemFixtures.Pkcs8(ecdsa);
        string notes = "web\n" + PemFixtures.Ed25519OpenSsh() + "\nbuild\n" + ecdsaKey;
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperNote("rec-servers", "Servers", notes)));
        string output = Path.Combine(directory, "merged.json");
        var policy = preferKeeper ? new MergePolicy(MergePolicyKind.PreferSource, "keeper.json") : new MergePolicy();
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper]) { Policy = policy, Output = new PipelineOutput(output, Plaintext: true) });

        // Assert
        result.HasUnresolvedConflicts.ShouldBeFalse();
        result.OutputWritten.ShouldBeTrue();
        VaultItem[] keys = [.. BitwardenJsonReader.Read(File.ReadAllText(output), "merged.json").Items.Where(item => item.Type == VaultItemType.SshKey)];
        keys.Select(key => key.Name).ShouldBe(["Servers SSH key", "Servers SSH key (2)"]);
        keys.Select(key => key.SshKey?.PrivateKey).ShouldBe([PemFixtures.Ed25519OpenSsh(), ecdsaKey]);
        keys.ShouldAllSatisfy(key => key.Fields.ShouldBeEmpty());
    }

    /// <summary>A second key stored with an SSH key is written as its own item.</summary>
    [Fact]
    public void Run_writes_a_second_key_stored_with_an_ssh_key_as_its_own_item()
    {
        // Arrange
        using var first = RSA.Create(2048);
        using var second = RSA.Create(2048);
        string privateKeys = PemFixtures.RsaPkcs1(first) + "\n" + PemFixtures.RsaPkcs1(second);
        string keeper = Write(directory, "keeper.json", KeeperJson(KeeperSshKey("rec-ssh", "Deploy keys", privateKeys, string.Empty)));
        string output = Path.Combine(directory, "merged.json");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());

        // Act
        var result = pipeline.Run(new PipelineRequest([keeper]) { Output = new PipelineOutput(output, Plaintext: true) });

        // Assert
        result.HasUnresolvedConflicts.ShouldBeFalse();
        result.OutputWritten.ShouldBeTrue();
        var items = BitwardenJsonReader.Read(File.ReadAllText(output), "merged.json").Items;
        items.Select(item => item.Name).ShouldBe(["Deploy keys", "Deploy keys SSH key"]);
        items.Select(item => item.SshKey?.PrivateKey).ShouldBe([PemFixtures.Pkcs8(first), PemFixtures.Pkcs8(second)]);
        items.ShouldAllSatisfy(item => item.Fields.ShouldBeEmpty());
    }

    /// <summary>An item changed by an accepted finding keeps its origin key.</summary>
    [Fact]
    public void Run_keeps_the_origin_key_of_an_item_changed_by_an_accepted_finding()
    {
        // Arrange
        string csv = Write(
            directory,
            "bitwarden.csv",
            "folder,favorite,type,name,notes,fields,reprompt,login_uri,login_username,login_password,login_totp\n"
            + ",,login,Example,Username: alice,,0,https://example.com,,fixture-csv-password,\n");
        var pipeline = new VaultPipeline(new TestPrompts(ExportPassword).Environment());
        var findings = pipeline.Run(new PipelineRequest([csv])).Findings.ShouldHaveSingleItem();
        var username = findings.Findings.ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Username);
        var decisions = PipelineDecisions.Findings(findings.OriginKey, new FindingChoice(username.Id, FindingDecision.Accepted));

        // Act
        var result = pipeline.Run(new PipelineRequest([csv]) { Decisions = decisions });

        // Assert
        result.Merged.Items.ShouldHaveSingleItem().Login.ShouldNotBeNull().Username.ShouldBe("alice");
        findings.OriginKey.ShouldStartWith("bitwarden|bitwarden.csv|content:", StringComparison.Ordinal);
        result.Findings.ShouldHaveSingleItem().OriginKey.ShouldBe(findings.OriginKey);
        result.Match.Groups.ShouldHaveSingleItem().MemberKeys.ShouldBe([findings.OriginKey]);
    }
}
