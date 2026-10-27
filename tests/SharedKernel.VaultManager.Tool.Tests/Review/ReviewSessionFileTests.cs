using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Review;
using PandaBear.VaultManager.Tool.Secrets;
using PandaBear.VaultManager.Tool.Tests.Pipeline;
using static PandaBear.VaultManager.Tool.Tests.Review.ReviewVaults;

namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>Tests that a review session saves decisions and writes the merged export to its files.</summary>
public sealed class ReviewSessionFileTests : IDisposable
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const string NotesPassword = "Gh-notes-Passw0rd!";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-review-files-").FullName;

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    /// <summary>Saved decisions read back as the same decisions, with pending entries for the findings still undecided.</summary>
    [Fact]
    public void Saved_decisions_read_back_as_the_same_decisions_with_pending_entries_for_what_is_left()
    {
        // Arrange
        string decisionsPath = Path.Combine(directory, "decisions.json");
        VaultItem github = CodesAndPasswordInNotes("rec-github", PipelineSecrets.GitHubRecoveryCodes(600), NotesPassword);
        VaultItem work = BitwardenLogin("bw-github", "GitHub", "bob", BitwardenPassword, "https://github.com", PipelineInputs.BitwardenRevision);
        var scanned = Scan(
            directory,
            KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com"),
            BitwardenLogin("bw-1", "Example", "alice", BitwardenPassword, "https://example.com"),
            github,
            work);
        var session = Session(scanned, files: new ReviewFiles(decisionsPath, null));
        string groupKey = GroupOf(session, KeeperKey).Key;
        session.ChooseMember(groupKey, FieldPaths.LoginPassword, BitwardenKey);
        string codesId = session.Findings(GroupOf(session, OriginKey(github)).Key).ShouldHaveSingleItem(finding => finding.Kind == SecretKind.RecoveryCodes).FindingId;
        session.DecideFinding(OriginKey(github), codesId, FindingDecision.Rejected);
        session.Link(OriginKey(github), OriginKey(work));

        // Act
        session.Save();
        var saved = DecisionsFile.Read(decisionsPath);
        var reopened = Session(scanned, saved, new ReviewFiles(decisionsPath, null));

        // Assert
        session.IsDirty.ShouldBeFalse();
        saved.Find(groupKey, FieldPaths.LoginPassword).ShouldBe(FieldChoice.Member(FieldPaths.LoginPassword, BitwardenKey));
        saved.FindFinding(OriginKey(github), codesId).ShouldBe(FindingDecision.Rejected);
        saved.Overrides.Links.ShouldBe([new OriginKeyPair(OriginKey(github), OriginKey(work))]);
        var githubDecisions = saved.Findings.ShouldHaveSingleItem();
        githubDecisions.ItemName.ShouldBe("GitHub");
        githubDecisions.Choices.ShouldHaveSingleItem(choice => choice.Decision == FindingDecision.Pending).FindingId.ShouldNotBe(codesId);
        DecisionsFile.Serialize(reopened.Decisions).ShouldBe(DecisionsFile.Serialize(session.Decisions));
        reopened.Pending.Fields.ShouldBeEmpty();
        reopened.Pending.FindingCount.ShouldBe(1);
        reopened.IsDirty.ShouldBeFalse();
    }

    /// <summary>The saved decisions file is readable only by its owner and holds none of the secret values.</summary>
    [Fact]
    public void Saved_decisions_file_is_readable_only_by_its_owner_and_holds_no_values()
    {
        // Arrange
        string decisionsPath = Path.Combine(directory, "decisions.json");
        string[] codes = PipelineSecrets.GitHubRecoveryCodes(610);
        var session = Session(
            Scan(directory, KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com"), BitwardenLogin("bw-1", "Example", "alice", BitwardenPassword, "https://example.com"), CodesAndPasswordInNotes("rec-github", codes, NotesPassword)),
            files: new ReviewFiles(decisionsPath, null));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);

        // Act
        session.Save();

        // Assert
        string text = File.ReadAllText(decisionsPath);
        string[] secrets = [KeeperPassword, BitwardenPassword, NotesPassword, .. codes];
        secrets.ShouldAllSatisfy(secret => text.ShouldNotContain(secret, StringComparison.Ordinal));
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(decisionsPath).ShouldBe(OwnerOnly);
        }
    }

    /// <summary>Saving a session that has no decisions file throws and leaves the decisions dirty.</summary>
    [Fact]
    public void Saving_without_a_decisions_file_is_refused()
    {
        // Arrange
        var session = Session(ConflictingLogins(directory));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        var action = () => session.Save();

        // Act
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("--decisions", StringComparison.Ordinal);
        session.IsDirty.ShouldBeTrue();
    }

    /// <summary>Writing is refused with a count of open fields while a field still needs a decision, and no file is created.</summary>
    [Fact]
    public void Nothing_is_written_while_a_field_needs_a_decision()
    {
        // Arrange
        string output = Path.Combine(directory, "merged.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(output)));
        var action = () => session.Write(ExportPassword);

        // Act
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("1 field(s) and 0 finding(s)", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
    }

    /// <summary>Writing is refused with a count of open findings while a proposed finding still needs a decision, and no file is created.</summary>
    [Fact]
    public void Nothing_is_written_while_a_proposed_finding_needs_a_decision()
    {
        // Arrange
        string output = Path.Combine(directory, "merged.json");
        var session = Session(Scan(directory, CodesAndPasswordInNotes("rec-github", PipelineSecrets.GitHubRecoveryCodes(620), NotesPassword)), files: new ReviewFiles(null, new PipelineOutput(output)));
        var action = () => session.Write(ExportPassword);

        // Act
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("0 field(s) and 1 finding(s)", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
    }

    /// <summary>The written export decrypts with the entered password, holds the chosen password and the other as history, and is readable only by its owner.</summary>
    [Fact]
    public void Written_export_decrypts_with_the_entered_password_and_is_readable_only_by_its_owner()
    {
        // Arrange
        string output = Path.Combine(directory, "merged.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(output)));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, BitwardenKey);

        // Act
        session.Write(ExportPassword);

        // Assert
        string json = PasswordProtectedExport.Decrypt(File.ReadAllText(output), ExportPassword);
        var item = BitwardenJsonReader.Read(json, "merged.json").Items.ShouldHaveSingleItem();
        item.Login.ShouldNotBeNull().Password.ShouldBe(BitwardenPassword);
        item.PasswordHistory.ShouldHaveSingleItem().Password.ShouldBe(KeeperPassword);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(output).ShouldBe(OwnerOnly);
        }
    }

    /// <summary>Writing with no password produces unencrypted Bitwarden JSON with the chosen password.</summary>
    [Fact]
    public void Plaintext_output_is_written_as_unencrypted_json_without_a_password()
    {
        // Arrange
        string output = Path.Combine(directory, "merged.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(output, Plaintext: true)));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);

        // Act
        session.Write(null);

        // Assert
        FormatDetector.Detect(output).ShouldBe(VaultFileFormat.BitwardenJson);
        BitwardenJsonReader.Read(File.ReadAllText(output), "merged.json").Items.ShouldHaveSingleItem().Login.ShouldNotBeNull().Password.ShouldBe(KeeperPassword);
    }

    /// <summary>An encrypted output refuses a missing, empty, or shorter than twelve characters password and creates no file.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short-pass")]
    public void Encrypted_output_needs_a_long_enough_password(string? password)
    {
        // Arrange
        string output = Path.Combine(directory, "merged.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(output)));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        var action = () => session.Write(password);

        // Act
        var exception = action.ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldContain("at least 12 characters", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
    }

    /// <summary>An existing output is kept when writing without force and is replaced when writing with force.</summary>
    [Fact]
    public void Existing_output_is_kept_without_force_and_replaced_with_force()
    {
        // Arrange
        string output = Path.Combine(directory, "merged.json");
        File.WriteAllText(output, "fixture existing output");
        var kept = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(output, Plaintext: true)));
        kept.ChooseMember(GroupOf(kept, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        var replaced = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(output, Plaintext: true, Force: true)));
        replaced.ChooseMember(GroupOf(replaced, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        var refused = () => kept.Write(null);

        // Act
        var exception = refused.ShouldThrow<IOException>();
        string afterRefusal = File.ReadAllText(output);
        replaced.Write(null);

        // Assert
        exception.Message.ShouldContain("already exists", StringComparison.Ordinal);
        afterRefusal.ShouldBe("fixture existing output");
        FormatDetector.Detect(output).ShouldBe(VaultFileFormat.BitwardenJson);
    }

    /// <summary>An output that is also an input file is never written and the input keeps its content.</summary>
    [Fact]
    public void Output_that_is_an_input_is_never_written()
    {
        // Arrange
        var scanned = ConflictingLogins(directory);
        string input = scanned.InputPaths[0];
        File.WriteAllText(input, "fixture input");
        var session = Session(scanned, files: new ReviewFiles(null, new PipelineOutput(input, Plaintext: true, Force: true)));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        var action = () => session.Write(null);

        // Act
        var exception = action.ShouldThrow<IOException>();

        // Assert
        exception.Message.ShouldContain("is also an input file", StringComparison.Ordinal);
        File.ReadAllText(input).ShouldBe("fixture input");
    }

    /// <summary>Writing a session that has no output file throws and creates no file.</summary>
    [Fact]
    public void Writing_without_an_output_file_is_refused()
    {
        // Arrange
        var session = Session(ConflictingLogins(directory));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        var action = () => session.Write(ExportPassword);

        // Act
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("--output", StringComparison.Ordinal);
        Directory.GetFiles(directory).ShouldBeEmpty();
    }
}
