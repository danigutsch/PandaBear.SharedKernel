using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Review;
using PandaBear.VaultManager.Tool.Secrets;
using PandaBear.VaultManager.Tool.Tests.Pipeline;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using static PandaBear.VaultManager.Tool.Tests.Review.ReviewVaults;

namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>Drives the review window headlessly with injected keys and checks the review state and the dialogs asked.</summary>
[Collection(HeadlessTerminal.Collection)]
public sealed class ReviewWindowTests : IDisposable
{
    private const string NotesPassword = "Gh-notes-Passw0rd!";
    private readonly string directory = Directory.CreateTempSubdirectory("vault-review-window-").FullName;

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    /// <summary>Enter in the fields pane takes the value of the selected member column for the selected field.</summary>
    [Fact]
    public void Enter_takes_the_value_of_the_selected_member_column()
    {
        // Arrange
        var session = Session(ConflictingLogins(directory));
        string groupKey = GroupOf(session, KeeperKey).Key;
        int passwordRow = ReviewRows.Field(session, groupKey, FieldPaths.LoginPassword);
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Fields.Key);
        HeadlessTerminal.Press(app, Key.CursorDown, passwordRow);
        HeadlessTerminal.Press(app, Key.CursorRight, ReviewKeys.ChooseMember.Key);
        app.End(token);

        // Assert
        session.Decisions.Find(groupKey, FieldPaths.LoginPassword).ShouldBe(FieldChoice.Member(FieldPaths.LoginPassword, BitwardenKey));
        session.Pending.IsEmpty.ShouldBeTrue();
    }

    /// <summary>Pressing a takes the selected member for every conflicting field.</summary>
    [Fact]
    public void Pressing_a_takes_the_selected_member_for_every_conflicting_field()
    {
        // Arrange
        VaultItem keeper = KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com") with
        {
            Notes = "fixture keeper notes"
        };
        VaultItem bitwarden = BitwardenLogin("bw-1", "Example account", "alice", BitwardenPassword, "https://example.com") with
        {
            Notes = "fixture bitwarden notes"
        };
        var session = Session(Scan(directory, keeper, bitwarden));
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Fields.Key, Key.CursorRight, ReviewKeys.ChooseForConflicts.Key);
        app.End(token);

        // Assert
        session.Decisions.Groups.ShouldHaveSingleItem().Choices.ShouldBe(
        [
            FieldChoice.Member(FieldPaths.Name, OriginKey(bitwarden)),
            FieldChoice.Member(FieldPaths.Notes, OriginKey(bitwarden)),
            FieldChoice.Member(FieldPaths.LoginPassword, OriginKey(bitwarden)),
        ]);
    }

    /// <summary>Pressing e drops the selected field and the delete key removes that decision again.</summary>
    [Fact]
    public void Pressing_e_drops_the_selected_field_and_delete_removes_the_decision()
    {
        // Arrange
        var session = Session(ConflictingLogins(directory));
        string groupKey = GroupOf(session, KeeperKey).Key;
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Fields.Key);
        HeadlessTerminal.Press(app, Key.CursorDown, ReviewRows.Field(session, groupKey, FieldPaths.LoginPassword));
        HeadlessTerminal.Press(app, ReviewKeys.ChooseEmpty.Key);
        FieldChoice? dropped = session.Decisions.Find(groupKey, FieldPaths.LoginPassword);
        HeadlessTerminal.Press(app, ReviewKeys.ClearChoice.Key);
        app.End(token);

        // Assert
        dropped.ShouldBe(FieldChoice.Empty(FieldPaths.LoginPassword));
        session.Decisions.Find(groupKey, FieldPaths.LoginPassword).ShouldBeNull();
        session.Pending.Fields.ShouldHaveSingleItem().FieldPath.ShouldBe(FieldPaths.LoginPassword);
    }

    /// <summary>Pressing v reveals the selected field and pressing it again masks it, without making the review dirty.</summary>
    [Fact]
    public void Pressing_v_reveals_and_masks_the_selected_field()
    {
        // Arrange
        var session = Session(ConflictingLogins(directory));
        string groupKey = GroupOf(session, KeeperKey).Key;
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Fields.Key);
        HeadlessTerminal.Press(app, Key.CursorDown, ReviewRows.Field(session, groupKey, FieldPaths.LoginPassword));
        HeadlessTerminal.Press(app, ReviewKeys.Reveal.Key);
        var revealed = FieldOf(session, groupKey, FieldPaths.LoginPassword);
        HeadlessTerminal.Press(app, ReviewKeys.Reveal.Key);
        app.End(token);

        // Assert
        revealed.Values.ShouldBe([KeeperPassword, BitwardenPassword]);
        FieldOf(session, groupKey, FieldPaths.LoginPassword).Values.ShouldBe([ReviewText.Mask, ReviewText.Mask]);
        session.IsDirty.ShouldBeFalse();
    }

    /// <summary>Pressing s splits the selected member out of its group and selects the group left behind.</summary>
    [Fact]
    public void Pressing_s_splits_the_selected_member_out_of_its_group()
    {
        // Arrange
        var session = Session(ConflictingLogins(directory));
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Fields.Key, Key.CursorRight, ReviewKeys.Split.Key);
        app.End(token);

        // Assert
        session.Decisions.Overrides.Splits.ShouldBe([new OriginKeyPair(BitwardenKey, KeeperKey)]);
        session.Groups(GroupFilter.All, null).Count.ShouldBe(2);
        window.SelectedGroup.ShouldNotBeNull().MemberKeys.ShouldBe([KeeperKey]);
    }

    /// <summary>The notes keys decide on the selected member's notes, and the share key asks for confirmation before sharing the rest.</summary>
    [Fact]
    public void Notes_keys_decide_on_the_selected_member_and_the_share_key_asks_before_sharing_the_rest()
    {
        // Arrange
        VaultItem wifi = KeeperNote("rec-3", "Wifi", "fixture wifi note");
        VaultItem router = KeeperNote("rec-4", "Router", "fixture router note");
        var session = Session(Scan(directory, wifi, router));
        var dialogs = new FakeDialogs { ConfirmAnswer = true };
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();
        string selected = window.SelectedGroup.ShouldNotBeNull().MemberKeys.ShouldHaveSingleItem();
        string other = selected == OriginKey(wifi) ? OriginKey(router) : OriginKey(wifi);

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Fields.Key, ReviewKeys.HideMemberNotes.Key, ReviewKeys.ShareNotes.Key);
        app.End(token);

        // Assert
        dialogs.Confirmations.ShouldBe(["Share notes"]);
        session.Decisions.FindNotes(selected).ShouldBe(NotesDecision.Hidden);
        session.Decisions.FindNotes(other).ShouldBe(NotesDecision.Share);
    }

    /// <summary>The finding keys accept, reject, clear, and reveal the selected finding.</summary>
    [Fact]
    public void Finding_keys_accept_reject_clear_and_reveal_the_selected_finding()
    {
        // Arrange
        VaultItem github = CodesAndPasswordInNotes("rec-github", PipelineSecrets.GitHubRecoveryCodes(700), NotesPassword);
        var session = Session(Scan(directory, github));
        string groupKey = GroupOf(session, OriginKey(github)).Key;
        var proposed = session.Findings(groupKey).ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Password);
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Findings.Key);
        HeadlessTerminal.Press(app, Key.CursorDown, ReviewRows.Finding(session, groupKey, proposed.FindingId));
        HeadlessTerminal.Press(app, ReviewKeys.Accept.Key);
        FindingDecision? accepted = session.Decisions.FindFinding(OriginKey(github), proposed.FindingId);
        HeadlessTerminal.Press(app, ReviewKeys.Reject.Key);
        FindingDecision? rejected = session.Decisions.FindFinding(OriginKey(github), proposed.FindingId);
        HeadlessTerminal.Press(app, ReviewKeys.ClearFinding.Key, ReviewKeys.RevealFinding.Key);
        app.End(token);

        // Assert
        accepted.ShouldBe(FindingDecision.Accepted);
        rejected.ShouldBe(FindingDecision.Rejected);
        session.Decisions.FindFinding(OriginKey(github), proposed.FindingId).ShouldBeNull();
        var shown = session.Findings(groupKey).ShouldHaveSingleItem(finding => finding.FindingId == proposed.FindingId);
        shown.IsRevealed.ShouldBeTrue();
        shown.Value.ShouldContain(NotesPassword, StringComparison.Ordinal);
    }

    /// <summary>Marking a group with m and pressing l on another group links the two and clears the mark.</summary>
    [Fact]
    public void Pressing_m_and_l_links_the_marked_group_with_the_selected_group()
    {
        // Arrange
        VaultItem keeper = KeeperLogin("rec-1", "GitHub", "alice", KeeperPassword, "https://github.com", PipelineInputs.KeeperModified);
        VaultItem bitwarden = BitwardenLogin("bw-1", "GitHub work", "alice.work", KeeperPassword, "https://github.com", PipelineInputs.BitwardenRevision);
        var session = Session(Scan(directory, keeper, bitwarden));
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Groups.Key, ReviewKeys.Mark.Key);
        string? marked = window.MarkedOriginKey;
        HeadlessTerminal.Press(app, Key.CursorDown, ReviewKeys.Link.Key);
        app.End(token);

        // Assert
        marked.ShouldBe(OriginKey(keeper));
        window.MarkedOriginKey.ShouldBeNull();
        session.Decisions.Overrides.Links.ShouldBe([new OriginKeyPair(OriginKey(keeper), OriginKey(bitwarden))]);
        window.SelectedGroup.ShouldNotBeNull().MemberKeys.ShouldBe([OriginKey(keeper), OriginKey(bitwarden)]);
    }

    /// <summary>F7 steps through the group filters and wraps around, and typing in the search field narrows the groups.</summary>
    [Fact]
    public void F7_changes_the_filter_and_the_search_field_narrows_the_groups()
    {
        // Arrange
        var session = Session(Scan(
            directory,
            KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com"),
            BitwardenLogin("bw-1", "Example", "alice", BitwardenPassword, "https://example.com"),
            KeeperNote("rec-2", "Wifi", "fixture wifi note")));
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        int all = window.ShownGroups.Count;
        HeadlessTerminal.Press(app, ReviewKeys.NextFilter.Key);
        GroupFilter filter = window.Filter;
        string[] pending = [.. window.ShownGroups.Select(group => group.Name)];
        HeadlessTerminal.Press(app, ReviewKeys.NextFilter.Key, 6);
        GroupFilter wrapped = window.Filter;
        HeadlessTerminal.Press(app, ReviewKeys.Search.Key);
        HeadlessTerminal.Type(app, "WIFI");
        app.End(token);

        // Assert
        all.ShouldBe(2);
        filter.ShouldBe(GroupFilter.Pending);
        pending.ShouldBe(["Example"]);
        wrapped.ShouldBe(GroupFilter.All);
        window.ShownGroups.ShouldHaveSingleItem().Name.ShouldBe("Wifi");
        window.SelectedGroup.ShouldNotBeNull().Name.ShouldBe("Wifi");
    }

    /// <summary>Ctrl+S saves the decisions file and leaves no unsaved changes.</summary>
    [Fact]
    public void Ctrl_s_saves_the_decisions_file()
    {
        // Arrange
        string decisionsPath = Path.Combine(directory, "decisions.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(decisionsPath, null));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        var dialogs = new FakeDialogs();
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Save.Key);
        app.End(token);

        // Assert
        session.IsDirty.ShouldBeFalse();
        DecisionsFile.Read(decisionsPath).Find(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword).ShouldBe(FieldChoice.Member(FieldPaths.LoginPassword, KeeperKey));
        dialogs.Errors.ShouldBeEmpty();
        dialogs.QuitQuestions.ShouldBeEmpty();
    }

    /// <summary>Ctrl+W asks for the export password once and writes the merged vault with the chosen values.</summary>
    [Fact]
    public void Ctrl_w_asks_for_the_export_password_and_writes_the_merged_vault()
    {
        // Arrange
        string output = Path.Combine(directory, "merged.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(output)));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, BitwardenKey);
        var dialogs = new FakeDialogs { Password = ExportPassword };
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Write.Key);
        app.End(token);

        // Assert
        dialogs.PasswordQuestions.ShouldBe(1);
        dialogs.Errors.ShouldBeEmpty();
        string json = PasswordProtectedExport.Decrypt(File.ReadAllText(output), ExportPassword);
        BitwardenJsonReader.Read(json, "merged.json").Items.ShouldHaveSingleItem().Login.ShouldNotBeNull().Password.ShouldBe(BitwardenPassword);
    }

    /// <summary>Ctrl+W reports how many attachments to upload after the import.</summary>
    [Fact]
    public void Ctrl_w_says_how_many_attachments_to_upload_after_the_import()
    {
        // Arrange
        string output = Path.Combine(directory, "merged.json");
        VaultItem keeper = KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com", PipelineInputs.KeeperModified) with
        {
            Attachments = [new AttachmentRef("codes.txt", 10, null)],
        };
        var session = Session(Scan(directory, keeper), files: new ReviewFiles(null, new PipelineOutput(output, Plaintext: true)));
        var dialogs = new FakeDialogs();
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Write.Key);
        app.End(token);

        // Assert
        dialogs.Errors.ShouldBeEmpty();
        session.Written.ShouldBeTrue();
        window.Message.ShouldContain("1 attachment(s) to upload after the import", StringComparison.Ordinal);
    }

    /// <summary>Ctrl+W with a decision still pending shows why in an error, asks no password, and writes nothing.</summary>
    [Fact]
    public void Ctrl_w_while_a_decision_is_pending_shows_why_and_asks_no_password()
    {
        // Arrange
        string output = Path.Combine(directory, "merged.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(output)));
        var dialogs = new FakeDialogs { Password = ExportPassword };
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Write.Key);
        app.End(token);

        // Assert
        dialogs.PasswordQuestions.ShouldBe(0);
        dialogs.Errors.ShouldHaveSingleItem().ShouldContain("1 field(s) and 0 finding(s) still need a decision", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
    }

    /// <summary>Write problems are shown as messages that contain no secret values and leave an existing output untouched.</summary>
    [Fact]
    public void Problems_show_messages_without_values()
    {
        // Arrange
        string output = Path.Combine(directory, "merged.json");
        File.WriteAllText(output, "fixture existing output");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(output, Plaintext: true)));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, BitwardenKey);
        var dialogs = new FakeDialogs();
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Write.Key);
        app.End(token);

        // Assert
        string problem = dialogs.Errors.ShouldHaveSingleItem();
        problem.ShouldContain("already exists", StringComparison.Ordinal);
        problem.ShouldNotContain(KeeperPassword, StringComparison.Ordinal);
        problem.ShouldNotContain(BitwardenPassword, StringComparison.Ordinal);
        File.ReadAllText(output).ShouldBe("fixture existing output");
    }

    /// <summary>F1 shows the list of every key.</summary>
    [Fact]
    public void F1_lists_every_key()
    {
        // Arrange
        var dialogs = new FakeDialogs();
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(Session(ConflictingLogins(directory)), dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Help.Key);
        app.End(token);

        // Assert
        dialogs.Help.ShouldHaveSingleItem().ShouldBe(ReviewKeys.All);
    }

    /// <summary>Quitting with unsaved decisions asks first, and canceling keeps the review open.</summary>
    [Fact]
    public void Quitting_with_unsaved_decisions_asks_first_and_cancel_keeps_the_review_open()
    {
        // Arrange
        var session = Session(ConflictingLogins(directory));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        var dialogs = new FakeDialogs().AnswerQuit(QuitChoice.Cancel, QuitChoice.Discard);
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Quit.Key);
        app.End(token);
        bool keptOpen = window.IsRunning;
        HeadlessTerminal.Press(app, ReviewKeys.Escape.Key);
        app.End(token);

        // Assert
        keptOpen.ShouldBeTrue();
        dialogs.QuitQuestions.ShouldBe([false, false]);
        window.IsRunning.ShouldBeFalse();
        session.IsDirty.ShouldBeTrue();
    }

    /// <summary>Choosing save when quitting writes the decisions file before the window closes.</summary>
    [Fact]
    public void Quitting_with_save_writes_the_decisions_file_first()
    {
        // Arrange
        string decisionsPath = Path.Combine(directory, "decisions.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(decisionsPath, null));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        var dialogs = new FakeDialogs().AnswerQuit(QuitChoice.Save);
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Quit.Key);
        app.End(token);

        // Assert
        dialogs.QuitQuestions.ShouldBe([true]);
        window.IsRunning.ShouldBeFalse();
        session.IsDirty.ShouldBeFalse();
        File.Exists(decisionsPath).ShouldBeTrue();
    }

    /// <summary>Quitting without changes, or stopping the window on cancellation, asks no question.</summary>
    [Fact]
    public void Quitting_without_changes_and_stopping_on_cancellation_ask_nothing()
    {
        // Arrange
        var unchanged = Session(ConflictingLogins(directory));
        var changed = Session(ConflictingLogins(directory));
        changed.ChooseMember(GroupOf(changed, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        var dialogs = new FakeDialogs();
        using IApplication app = HeadlessTerminal.Start();
        using var unchangedWindow = new ReviewWindow(unchanged, dialogs);
        using var changedWindow = new ReviewWindow(changed, dialogs);

        // Act
        SessionToken unchangedToken = app.Begin(unchangedWindow).ShouldNotBeNull();
        HeadlessTerminal.Press(app, ReviewKeys.Escape.Key);
        app.End(unchangedToken);
        SessionToken changedToken = app.Begin(changedWindow).ShouldNotBeNull();
        changedWindow.Stop();
        app.End(changedToken);

        // Assert
        dialogs.QuitQuestions.ShouldBeEmpty();
        unchangedWindow.IsRunning.ShouldBeFalse();
        changedWindow.IsRunning.ShouldBeFalse();
    }
}
