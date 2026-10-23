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

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void Enter_takes_the_value_of_the_selected_member_column()
    {
        var session = Session(ConflictingLogins(directory));
        string groupKey = GroupOf(session, KeeperKey).Key;
        int passwordRow = ReviewRows.Field(session, groupKey, FieldPaths.LoginPassword);
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        HeadlessTerminal.Press(app, ReviewKeys.Fields.Key);
        HeadlessTerminal.Press(app, Key.CursorDown, passwordRow);
        HeadlessTerminal.Press(app, Key.CursorRight, ReviewKeys.ChooseMember.Key);
        app.End(token);

        session.Decisions.Find(groupKey, FieldPaths.LoginPassword).ShouldBe(FieldChoice.Member(FieldPaths.LoginPassword, BitwardenKey));
        session.Pending.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Pressing_a_takes_the_selected_member_for_every_conflicting_field()
    {
        VaultItem keeper = KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com") with { Notes = "fixture keeper notes" };
        VaultItem bitwarden = BitwardenLogin("bw-1", "Example account", "alice", BitwardenPassword, "https://example.com") with { Notes = "fixture bitwarden notes" };
        var session = Session(Scan(directory, keeper, bitwarden));
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        HeadlessTerminal.Press(app, ReviewKeys.Fields.Key, Key.CursorRight, ReviewKeys.ChooseForConflicts.Key);
        app.End(token);

        session.Decisions.Groups.ShouldHaveSingleItem().Choices.ShouldBe(
        [
            FieldChoice.Member(FieldPaths.Name, OriginKey(bitwarden)),
            FieldChoice.Member(FieldPaths.Notes, OriginKey(bitwarden)),
            FieldChoice.Member(FieldPaths.LoginPassword, OriginKey(bitwarden)),
        ]);
    }

    [Fact]
    public void Pressing_e_drops_the_selected_field_and_delete_removes_the_decision()
    {
        var session = Session(ConflictingLogins(directory));
        string groupKey = GroupOf(session, KeeperKey).Key;
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        HeadlessTerminal.Press(app, ReviewKeys.Fields.Key);
        HeadlessTerminal.Press(app, Key.CursorDown, ReviewRows.Field(session, groupKey, FieldPaths.LoginPassword));
        HeadlessTerminal.Press(app, ReviewKeys.ChooseEmpty.Key);
        FieldChoice? dropped = session.Decisions.Find(groupKey, FieldPaths.LoginPassword);
        HeadlessTerminal.Press(app, ReviewKeys.ClearChoice.Key);
        app.End(token);

        dropped.ShouldBe(FieldChoice.Empty(FieldPaths.LoginPassword));
        session.Decisions.Find(groupKey, FieldPaths.LoginPassword).ShouldBeNull();
        session.Pending.Fields.ShouldHaveSingleItem().FieldPath.ShouldBe(FieldPaths.LoginPassword);
    }

    [Fact]
    public void Pressing_v_reveals_and_masks_the_selected_field()
    {
        var session = Session(ConflictingLogins(directory));
        string groupKey = GroupOf(session, KeeperKey).Key;
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        HeadlessTerminal.Press(app, ReviewKeys.Fields.Key);
        HeadlessTerminal.Press(app, Key.CursorDown, ReviewRows.Field(session, groupKey, FieldPaths.LoginPassword));
        HeadlessTerminal.Press(app, ReviewKeys.Reveal.Key);
        var revealed = FieldOf(session, groupKey, FieldPaths.LoginPassword);
        HeadlessTerminal.Press(app, ReviewKeys.Reveal.Key);
        app.End(token);

        revealed.Values.ShouldBe([KeeperPassword, BitwardenPassword]);
        FieldOf(session, groupKey, FieldPaths.LoginPassword).Values.ShouldBe([ReviewText.Mask, ReviewText.Mask]);
        session.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void Pressing_s_splits_the_selected_member_out_of_its_group()
    {
        var session = Session(ConflictingLogins(directory));
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        HeadlessTerminal.Press(app, ReviewKeys.Fields.Key, Key.CursorRight, ReviewKeys.Split.Key);
        app.End(token);

        session.Decisions.Overrides.Splits.ShouldBe([new OriginKeyPair(BitwardenKey, KeeperKey)]);
        session.Groups(GroupFilter.All, null).Count.ShouldBe(2);
        window.SelectedGroup.ShouldNotBeNull().MemberKeys.ShouldBe([KeeperKey]);
    }

    [Fact]
    public void Finding_keys_accept_reject_clear_and_reveal_the_selected_finding()
    {
        VaultItem github = CodesAndPasswordInNotes("rec-github", PipelineSecrets.GitHubRecoveryCodes(700), NotesPassword);
        var session = Session(Scan(directory, github));
        string groupKey = GroupOf(session, OriginKey(github)).Key;
        var proposed = session.Findings(groupKey).ShouldHaveSingleItem(finding => finding.Kind == SecretKind.Password);
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        HeadlessTerminal.Press(app, ReviewKeys.Findings.Key);
        HeadlessTerminal.Press(app, Key.CursorDown, ReviewRows.Finding(session, groupKey, proposed.FindingId));
        HeadlessTerminal.Press(app, ReviewKeys.Accept.Key);
        FindingDecision? accepted = session.Decisions.FindFinding(OriginKey(github), proposed.FindingId);
        HeadlessTerminal.Press(app, ReviewKeys.Reject.Key);
        FindingDecision? rejected = session.Decisions.FindFinding(OriginKey(github), proposed.FindingId);
        HeadlessTerminal.Press(app, ReviewKeys.ClearFinding.Key, ReviewKeys.RevealFinding.Key);
        app.End(token);

        accepted.ShouldBe(FindingDecision.Accepted);
        rejected.ShouldBe(FindingDecision.Rejected);
        session.Decisions.FindFinding(OriginKey(github), proposed.FindingId).ShouldBeNull();
        var shown = session.Findings(groupKey).ShouldHaveSingleItem(finding => finding.FindingId == proposed.FindingId);
        shown.IsRevealed.ShouldBeTrue();
        shown.Value.ShouldContain(NotesPassword, StringComparison.Ordinal);
    }

    [Fact]
    public void Pressing_m_and_l_links_the_marked_group_with_the_selected_group()
    {
        VaultItem keeper = KeeperLogin("rec-1", "GitHub", "alice", KeeperPassword, "https://github.com", PipelineInputs.KeeperModified);
        VaultItem bitwarden = BitwardenLogin("bw-1", "GitHub work", "alice.work", KeeperPassword, "https://github.com", PipelineInputs.BitwardenRevision);
        var session = Session(Scan(directory, keeper, bitwarden));
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        HeadlessTerminal.Press(app, ReviewKeys.Groups.Key, ReviewKeys.Mark.Key);
        string? marked = window.MarkedOriginKey;
        HeadlessTerminal.Press(app, Key.CursorDown, ReviewKeys.Link.Key);
        app.End(token);

        marked.ShouldBe(OriginKey(keeper));
        window.MarkedOriginKey.ShouldBeNull();
        session.Decisions.Overrides.Links.ShouldBe([new OriginKeyPair(OriginKey(keeper), OriginKey(bitwarden))]);
        window.SelectedGroup.ShouldNotBeNull().MemberKeys.ShouldBe([OriginKey(keeper), OriginKey(bitwarden)]);
    }

    [Fact]
    public void F7_changes_the_filter_and_the_search_field_narrows_the_groups()
    {
        var session = Session(Scan(
            directory,
            KeeperLogin("rec-1", "Example", "alice", KeeperPassword, "https://example.com"),
            BitwardenLogin("bw-1", "Example", "alice", BitwardenPassword, "https://example.com"),
            KeeperNote("rec-2", "Wifi", "fixture wifi note")));
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, new FakeDialogs());
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        int all = window.ShownGroups.Count;
        HeadlessTerminal.Press(app, ReviewKeys.NextFilter.Key);
        GroupFilter filter = window.Filter;
        string[] pending = [.. window.ShownGroups.Select(group => group.Name)];
        HeadlessTerminal.Press(app, ReviewKeys.NextFilter.Key, 6);
        GroupFilter wrapped = window.Filter;
        HeadlessTerminal.Press(app, ReviewKeys.Search.Key);
        HeadlessTerminal.Type(app, "WIFI");
        app.End(token);

        all.ShouldBe(2);
        filter.ShouldBe(GroupFilter.Pending);
        pending.ShouldBe(["Example"]);
        wrapped.ShouldBe(GroupFilter.All);
        window.ShownGroups.ShouldHaveSingleItem().Name.ShouldBe("Wifi");
        window.SelectedGroup.ShouldNotBeNull().Name.ShouldBe("Wifi");
    }

    [Fact]
    public void Ctrl_s_saves_the_decisions_file()
    {
        string decisionsPath = Path.Combine(directory, "decisions.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(decisionsPath, null));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        var dialogs = new FakeDialogs();
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        HeadlessTerminal.Press(app, ReviewKeys.Save.Key);
        app.End(token);

        session.IsDirty.ShouldBeFalse();
        DecisionsFile.Read(decisionsPath).Find(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword).ShouldBe(FieldChoice.Member(FieldPaths.LoginPassword, KeeperKey));
        dialogs.Errors.ShouldBeEmpty();
        dialogs.QuitQuestions.ShouldBeEmpty();
    }

    [Fact]
    public void Ctrl_w_asks_for_the_export_password_and_writes_the_merged_vault()
    {
        string output = Path.Combine(directory, "merged.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(output)));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, BitwardenKey);
        var dialogs = new FakeDialogs { Password = ExportPassword };
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        HeadlessTerminal.Press(app, ReviewKeys.Write.Key);
        app.End(token);

        dialogs.PasswordQuestions.ShouldBe(1);
        dialogs.Errors.ShouldBeEmpty();
        string json = PasswordProtectedExport.Decrypt(File.ReadAllText(output), ExportPassword);
        BitwardenJsonReader.Read(json, "merged.json").Items.ShouldHaveSingleItem().Login.ShouldNotBeNull().Password.ShouldBe(BitwardenPassword);
    }

    [Fact]
    public void Ctrl_w_says_how_many_attachments_to_upload_after_the_import()
    {
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

        HeadlessTerminal.Press(app, ReviewKeys.Write.Key);
        app.End(token);

        dialogs.Errors.ShouldBeEmpty();
        session.Written.ShouldBeTrue();
        window.Message.ShouldContain("1 attachment(s) to upload after the import", StringComparison.Ordinal);
    }

    [Fact]
    public void Ctrl_w_while_a_decision_is_pending_shows_why_and_asks_no_password()
    {
        string output = Path.Combine(directory, "merged.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(output)));
        var dialogs = new FakeDialogs { Password = ExportPassword };
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        HeadlessTerminal.Press(app, ReviewKeys.Write.Key);
        app.End(token);

        dialogs.PasswordQuestions.ShouldBe(0);
        dialogs.Errors.ShouldHaveSingleItem().ShouldContain("1 field(s) and 0 finding(s) still need a decision", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public void Problems_show_messages_without_values()
    {
        string output = Path.Combine(directory, "merged.json");
        File.WriteAllText(output, "fixture existing output");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(output, Plaintext: true)));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, BitwardenKey);
        var dialogs = new FakeDialogs();
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        HeadlessTerminal.Press(app, ReviewKeys.Write.Key);
        app.End(token);

        string problem = dialogs.Errors.ShouldHaveSingleItem();
        problem.ShouldContain("already exists", StringComparison.Ordinal);
        problem.ShouldNotContain(KeeperPassword, StringComparison.Ordinal);
        problem.ShouldNotContain(BitwardenPassword, StringComparison.Ordinal);
        File.ReadAllText(output).ShouldBe("fixture existing output");
    }

    [Fact]
    public void F1_lists_every_key()
    {
        var dialogs = new FakeDialogs();
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(Session(ConflictingLogins(directory)), dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        HeadlessTerminal.Press(app, ReviewKeys.Help.Key);
        app.End(token);

        dialogs.Help.ShouldHaveSingleItem().ShouldBe(ReviewKeys.All);
    }

    [Fact]
    public void Quitting_with_unsaved_decisions_asks_first_and_cancel_keeps_the_review_open()
    {
        var session = Session(ConflictingLogins(directory));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        var dialogs = new FakeDialogs().AnswerQuit(QuitChoice.Cancel, QuitChoice.Discard);
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        HeadlessTerminal.Press(app, ReviewKeys.Quit.Key);
        app.End(token);
        bool keptOpen = window.IsRunning;
        HeadlessTerminal.Press(app, ReviewKeys.Escape.Key);
        app.End(token);

        keptOpen.ShouldBeTrue();
        dialogs.QuitQuestions.ShouldBe([false, false]);
        window.IsRunning.ShouldBeFalse();
        session.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Quitting_with_save_writes_the_decisions_file_first()
    {
        string decisionsPath = Path.Combine(directory, "decisions.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(decisionsPath, null));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        var dialogs = new FakeDialogs().AnswerQuit(QuitChoice.Save);
        using IApplication app = HeadlessTerminal.Start();
        using var window = new ReviewWindow(session, dialogs);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        HeadlessTerminal.Press(app, ReviewKeys.Quit.Key);
        app.End(token);

        dialogs.QuitQuestions.ShouldBe([true]);
        window.IsRunning.ShouldBeFalse();
        session.IsDirty.ShouldBeFalse();
        File.Exists(decisionsPath).ShouldBeTrue();
    }

    [Fact]
    public void Quitting_without_changes_and_stopping_on_cancellation_ask_nothing()
    {
        var unchanged = Session(ConflictingLogins(directory));
        var changed = Session(ConflictingLogins(directory));
        changed.ChooseMember(GroupOf(changed, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        var dialogs = new FakeDialogs();
        using IApplication app = HeadlessTerminal.Start();
        using var unchangedWindow = new ReviewWindow(unchanged, dialogs);
        using var changedWindow = new ReviewWindow(changed, dialogs);

        SessionToken unchangedToken = app.Begin(unchangedWindow).ShouldNotBeNull();
        HeadlessTerminal.Press(app, ReviewKeys.Escape.Key);
        app.End(unchangedToken);
        SessionToken changedToken = app.Begin(changedWindow).ShouldNotBeNull();
        changedWindow.Stop();
        app.End(changedToken);

        dialogs.QuitQuestions.ShouldBeEmpty();
        unchangedWindow.IsRunning.ShouldBeFalse();
        changedWindow.IsRunning.ShouldBeFalse();
    }
}
