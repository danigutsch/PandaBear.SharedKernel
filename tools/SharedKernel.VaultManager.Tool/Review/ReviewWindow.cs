using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using PandaBear.VaultManager.Tool.Cli;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Pipeline;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace PandaBear.VaultManager.Tool.Review;

/// <summary>
/// The review window: the groups on the left, and tabs with the field differences and the findings of the selected group
/// on the right. The window only shows the state of a <see cref="ReviewSession"/> and forwards keys to it; every key is
/// listed in <see cref="ReviewKeys"/>. Messages hold names, keys, field paths, and counts, never values.
/// </summary>
[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "The fields are subviews, which View.Dispose disposes with the window.")]
internal sealed class ReviewWindow : Window
{
    private const int MemberColumn = 2;

    private static readonly GroupFilter[] FilterOrder = Enum.GetValues<GroupFilter>();

    private readonly ReviewSession session;
    private readonly IReviewDialogs dialogs;
    private readonly FrameView groupsFrame;
    private readonly TextField search;
    private readonly ListView groupsList;
    private readonly Label header;
    private readonly Tabs tabs;
    private readonly View fieldsTab;
    private readonly TableView fieldsTable;
    private readonly View findingsTab;
    private readonly TableView findingsTable;
    private readonly Label messageLine;
    private readonly (ReviewKey Key, Action Action)[] groupKeys;
    private readonly (ReviewKey Key, Action Action)[] fieldKeys;
    private readonly (ReviewKey Key, Action Action)[] findingKeys;
    private IReadOnlyList<ReviewMember> members = [];
    private IReadOnlyList<ReviewField> fields = [];
    private IReadOnlyList<ReviewFinding> findings = [];
    private string? anchorOriginKey;
    private bool refreshing;
    private bool stopping;

    /// <summary>Builds the window for a review.</summary>
    /// <param name="session">Review state.</param>
    /// <param name="dialogs">Questions and messages the window shows.</param>
    public ReviewWindow(ReviewSession session, IReviewDialogs dialogs)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(dialogs);
        this.session = session;
        this.dialogs = dialogs;

        groupsFrame = new FrameView { X = 0, Y = 0, Width = Dim.Percent(35), Height = Dim.Fill(2) };
        var searchLabel = new Label { X = 0, Y = 0, Text = "Search:" };
        search = new TextField { X = Pos.Right(searchLabel) + 1, Y = 0, Width = Dim.Fill() };
        groupsList = new ListView { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(), KeystrokeNavigator = null! };
        groupsFrame.Add(searchLabel, search, groupsList);

        header = new Label { X = Pos.Right(groupsFrame) + 1, Y = 0, Width = Dim.Fill(), Height = 1 };
        fieldsTable = new TableView { Width = Dim.Fill(), Height = Dim.Fill(), CollectionNavigator = null!, FullRowSelect = false };
        fieldsTab = new View { Title = "Fields", CanFocus = true, Width = Dim.Fill(), Height = Dim.Fill() };
        fieldsTab.Add(fieldsTable);
        findingsTable = new TableView { Width = Dim.Fill(), Height = Dim.Fill(), CollectionNavigator = null!, FullRowSelect = true };
        findingsTab = new View { Title = "Findings", CanFocus = true, Width = Dim.Fill(), Height = Dim.Fill() };
        findingsTab.Add(findingsTable);
        tabs = new Tabs { X = Pos.Right(groupsFrame), Y = 1, Width = Dim.Fill(), Height = Dim.Fill(2) };
        tabs.Add(fieldsTab, findingsTab);

        messageLine = new Label { X = 0, Y = Pos.AnchorEnd(2), Width = Dim.Fill(), Height = 1, Text = "F1 lists every key." };
        var statusBar = new StatusBar();
        statusBar.Add(Shortcut(ReviewKeys.Help, "Help", ShowHelp));
        statusBar.Add(Shortcut(ReviewKeys.Groups, "Groups", FocusGroups));
        statusBar.Add(Shortcut(ReviewKeys.Fields, "Fields", FocusFields));
        statusBar.Add(Shortcut(ReviewKeys.Findings, "Findings", FocusFindings));
        statusBar.Add(Shortcut(ReviewKeys.NextFilter, "Filter", NextFilter));
        statusBar.Add(Shortcut(ReviewKeys.Search, "Search", FocusSearch));
        statusBar.Add(Shortcut(ReviewKeys.Save, "Save", Save));
        statusBar.Add(Shortcut(ReviewKeys.Write, "Write", Write));
        statusBar.Add(Shortcut(ReviewKeys.Quit, "Quit", Quit));
        Add(groupsFrame, header, tabs, messageLine, statusBar);

        groupKeys = [(ReviewKeys.OpenGroup, FocusFields), (ReviewKeys.Mark, Mark), (ReviewKeys.Link, LinkMarked)];
        fieldKeys =
        [
            (ReviewKeys.ChooseMember, ChooseMember),
            (ReviewKeys.ChooseForConflicts, ChooseForConflicts),
            (ReviewKeys.ChooseEmpty, ChooseEmpty),
            (ReviewKeys.ClearChoice, ClearChoice),
            (ReviewKeys.Reveal, RevealField),
            (ReviewKeys.Split, SplitMember),
        ];
        findingKeys =
        [
            (ReviewKeys.Accept, () => Decide(FindingDecision.Accepted)),
            (ReviewKeys.Reject, () => Decide(FindingDecision.Rejected)),
            (ReviewKeys.ClearFinding, ClearFinding),
            (ReviewKeys.RevealFinding, RevealFinding),
        ];
        search.TextChanged += (_, _) => RefreshGroups();
        groupsList.ValueChanged += (_, _) => GroupSelected();
        groupsList.KeyDown += (_, key) => Dispatch(key, groupKeys);
        fieldsTable.KeyDown += (_, key) => Dispatch(key, fieldKeys);
        findingsTable.KeyDown += (_, key) => Dispatch(key, findingKeys);
        RefreshGroups();
    }

    /// <summary>Status filter of the groups list.</summary>
    public GroupFilter Filter
    {
        get; private set;
    }

    /// <summary>Groups the list shows, in list order.</summary>
    public IReadOnlyList<ReviewGroup> ShownGroups { get; private set; } = [];

    /// <summary>Selected group, or <see langword="null"/> when the list is empty.</summary>
    public ReviewGroup? SelectedGroup
    {
        get; private set;
    }

    /// <summary>Origin key of the item marked for a link, or <see langword="null"/> when none is marked.</summary>
    public string? MarkedOriginKey
    {
        get; private set;
    }

    /// <summary>Message line: what the last action did, without values.</summary>
    public string Message => messageLine.Text;

    /// <summary>
    /// Stops the window without asking about unsaved decisions, for example when the command is canceled. Open dialogs,
    /// such as the key list or the export password, close first without an answer, so the action that opened them ends
    /// and the window stops after it. Call it on the thread that runs the window.
    /// </summary>
    public void Stop()
    {
        stopping = true;
        if (App is not { } app)
        {
            return;
        }

        // A dialog runs its own loop inside the window's loop, so stopping only the window would wait for the dialog. The
        // session stack lists the top session first.
        if (app.SessionStack is { } sessions)
        {
            foreach (SessionToken token in sessions)
            {
                if (token.Runnable is { } runnable && !ReferenceEquals(runnable, this))
                {
                    app.RequestStop(runnable);
                }
            }
        }

        app.RequestStop(this);
    }

    /// <inheritdoc/>
    protected override bool OnIsRunningChanging(bool oldIsRunning, bool newIsRunning)
    {
        if (!newIsRunning && !stopping && session.IsDirty)
        {
            // Without the question the decisions would be lost, so the review stays open until the question can be shown
            // or the decisions are saved.
            if (dialogs.Unavailable is { } unavailable)
            {
                Notify(unavailable + " The review stays open.");
                return true;
            }

            QuitChoice choice = dialogs.AskQuit(session.Files.DecisionsPath is not null);
            bool keepOpen = choice == QuitChoice.Cancel || (choice == QuitChoice.Save && !TryRun(SaveDecisions));

            // Stop closes the question, or a problem shown while saving, without an answer; the window stops anyway.
            if (keepOpen && !stopping)
            {
                Notify("The review stays open.");
                return true;
            }
        }

        stopping |= !newIsRunning;
        return base.OnIsRunningChanging(oldIsRunning, newIsRunning);
    }

    /// <inheritdoc/>
    protected override void OnIsRunningChanged(bool newIsRunning)
    {
        base.OnIsRunningChanged(newIsRunning);
        if (newIsRunning)
        {
            groupsList.SetFocus();
        }
    }

    private static Shortcut Shortcut(ReviewKey key, string title, Action action) => new(key.Key, title, action, string.Empty);

    // Runs the action bound to a key that nothing handled yet. A letter key works with Shift or Caps Lock too; other keys
    // match exactly.
    private static void Dispatch(Key pressed, IReadOnlyList<(ReviewKey Key, Action Action)> bindings)
    {
        if (pressed.Handled)
        {
            return;
        }

        foreach ((ReviewKey bound, Action action) in bindings)
        {
            if (pressed == bound.Key || (bound.Key.IsKeyCodeAtoZ && pressed.NoShift == bound.Key))
            {
                pressed.Handled = true;
                action();
                return;
            }
        }
    }

    // ArgumentException appends the parameter name to its message, which tells the user nothing.
    private static string Describe(Exception exception) =>
        exception is ArgumentException { ParamName: { } name } argument
            ? argument.Message.Replace(" (Parameter '" + name + "')", string.Empty, StringComparison.Ordinal)
            : exception.Message;

    private static string FilterName(GroupFilter filter) => filter switch
    {
        GroupFilter.All => "all",
        GroupFilter.Pending => "pending",
        GroupFilter.Conflict => "conflict",
        GroupFilter.Suggested => "suggested",
        GroupFilter.Identical => "identical",
        GroupFilter.Unique => "unique",
        GroupFilter.HasFindings => "has findings",
        _ => filter.ToString(),
    };

    private static string Line(ReviewGroup group)
    {
        char marker = group.IsPending ? '!'
            : group.ConflictingFields.Count > 0 ? '~'
            : group.Status == MatchStatus.Identical ? '='
            : ' ';
        string count = group.MemberKeys.Count > 1 ? string.Create(CultureInfo.InvariantCulture, $" ({group.MemberKeys.Count})") : string.Empty;
        return marker + " " + ReviewText.OneLine(group.Name) + count;
    }

    private static string NoMemberChoice(ReviewField field, ReviewGroup group) =>
        group.MemberKeys.Count < 2 ? "A group with one item takes no decisions."
        : field.AcceptsEmpty ? field.Label + " keeps the entries of every item; press e to drop the whole list."
        : field.Label + " follows the newest item.";

    private void Notify(string text) => messageLine.Text = ReviewText.OneLine(text);

    // Runs a session operation and shows what it did. Session and pipeline errors carry names, keys, field paths, and file
    // paths, never values; anything else is unexpected and ends the review.
    private bool TryRun(Func<string> action)
    {
        string done;
        try
        {
            done = action();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowError(ReviewText.OneLine(Describe(exception)));
            RefreshGroups();
            return false;
        }

        Notify(done);
        RefreshGroups();
        return true;
    }

    private void ShowHelp()
    {
        if (dialogs.Unavailable is { } unavailable)
        {
            Notify(unavailable);
            return;
        }

        dialogs.ShowHelp(ReviewKeys.All);
    }

    // Shows a problem in a dialog, or on the message line when no dialog can be shown.
    private void ShowError(string message)
    {
        if (dialogs.Unavailable is not null)
        {
            Notify(message);
            return;
        }

        dialogs.ShowError(message);
    }

    private void FocusGroups() => groupsList.SetFocus();

    private void FocusFields()
    {
        tabs.Value = fieldsTab;
        fieldsTable.SetFocus();
    }

    private void FocusFindings()
    {
        tabs.Value = findingsTab;
        findingsTable.SetFocus();
    }

    private void FocusSearch() => search.SetFocus();

    private void NextFilter()
    {
        Filter = FilterOrder[(Array.IndexOf(FilterOrder, Filter) + 1) % FilterOrder.Length];
        RefreshGroups();
        Notify("Showing " + FilterName(Filter) + " groups.");
    }

    private void Save() => TryRun(SaveDecisions);

    private string SaveDecisions()
    {
        session.Save();
        return "Saved the decisions to " + session.Files.DecisionsPath + ".";
    }

    private void Write()
    {
        if (session.WriteBlocker is { } blocker)
        {
            ShowError(blocker);
            return;
        }

        PipelineOutput output = session.Files.Output!;
        string? password = null;
        if (!output.Plaintext)
        {
            if (dialogs.Unavailable is { } unavailable)
            {
                Notify(unavailable + " Nothing was written.");
                return;
            }

            password = dialogs.AskExportPassword();
            if (password is null)
            {
                Notify("Writing the merged vault was canceled.");
                return;
            }
        }

        TryRun(() =>
        {
            session.Write(password);
            return Written(output, session.WriteResult!);
        });
    }

    // The import cannot carry attachments, and the writer's warnings concern the written file, so the message says what
    // the review prints about them after it ends.
    private static string Written(PipelineOutput output, PipelineResult written)
    {
        string text = output.Plaintext
            ? "Wrote the merged vault to " + output.Path + " as unencrypted JSON. Delete it after the import."
            : "Wrote the merged vault to " + output.Path + " as a password-protected export.";
        if (written.ManualAttachments.Count > 0)
        {
            text += string.Create(CultureInfo.InvariantCulture, $" {written.ManualAttachments.Count} attachment(s) to upload after the import are listed when you quit.");
        }

        int warnings = written.WriteWarnings.Count + written.PlacementProblems.Count;
        if (warnings > 0)
        {
            text += string.Create(CultureInfo.InvariantCulture, $" {warnings} warning(s) are printed when you quit.");
        }

        return text;
    }

    private void Quit() => App?.RequestStop(this);

    private void Mark()
    {
        if (SelectedGroup is not { } group)
        {
            return;
        }

        MarkedOriginKey = group.MemberKeys[0];
        Notify("Marked " + group.Name + ". Select another group and press l to link the two.");
    }

    private void LinkMarked()
    {
        if (SelectedGroup is not { } group)
        {
            return;
        }

        if (MarkedOriginKey is not { } marked)
        {
            Notify("Mark a group with m first.");
            return;
        }

        if (group.MemberKeys.Contains(marked, StringComparer.Ordinal))
        {
            Notify("Select a group other than the marked one.");
            return;
        }

        string target = group.MemberKeys[0];
        TryRun(() =>
        {
            bool linked = session.Link(marked, target);
            MarkedOriginKey = null;
            anchorOriginKey = marked;
            return linked ? "Linked the two groups." : "The link is kept, but a split keeps items of the two groups apart.";
        });
    }

    private void ChooseMember()
    {
        if (SelectedGroup is not { } group || SelectedField() is not { } field)
        {
            return;
        }

        if (!field.AcceptsMember)
        {
            Notify(NoMemberChoice(field, group));
        }
        else if (SelectedMember() is not { } member)
        {
            Notify("Move to the column of the item whose value to take.");
        }
        else
        {
            TryRun(() =>
            {
                session.ChooseMember(group.Key, field.FieldPath, member.OriginKey);
                return field.Label + " takes the value of " + Header(member) + ".";
            });
        }
    }

    private void ChooseForConflicts()
    {
        if (SelectedGroup is not { } group)
        {
            return;
        }

        if (SelectedMember() is not { } member)
        {
            Notify("Move to the column of the item whose values to take.");
            return;
        }

        TryRun(() =>
        {
            int decided = session.ChooseMemberForConflicts(group.Key, member.OriginKey);
            return string.Create(CultureInfo.InvariantCulture, $"{decided} conflicting field(s) take the values of {Header(member)}.");
        });
    }

    private void ChooseEmpty()
    {
        if (SelectedGroup is not { } group || SelectedField() is not { } field)
        {
            return;
        }

        if (!field.AcceptsEmpty)
        {
            Notify(group.MemberKeys.Count < 2 ? "A group with one item takes no decisions." : field.Label + " cannot be dropped.");
            return;
        }

        TryRun(() =>
        {
            session.ChooseEmpty(group.Key, field.FieldPath);
            return field.Label + " is dropped from every item.";
        });
    }

    private void ClearChoice()
    {
        if (SelectedGroup is not { } group || SelectedField() is not { } field)
        {
            return;
        }

        if (field.Choice is null)
        {
            Notify(field.Label + " has no decision.");
            return;
        }

        TryRun(() =>
        {
            session.ClearChoice(group.Key, field.FieldPath);
            return field.Label + " follows the newest item again.";
        });
    }

    private void RevealField()
    {
        if (SelectedGroup is not { } group || SelectedField() is not { } field)
        {
            return;
        }

        if (!field.IsSecret)
        {
            Notify(field.Label + " is not masked.");
            return;
        }

        TryRun(() => session.ToggleReveal(group.Key, field.FieldPath)
            ? field.Label + " is revealed. Press v to mask it again."
            : field.Label + " is masked.");
    }

    private void SplitMember()
    {
        if (SelectedGroup is not { } group)
        {
            return;
        }

        if (SelectedMember() is not { } member)
        {
            Notify("Move to the column of the item to split out of the group.");
            return;
        }

        if (group.MemberKeys.Count < 2)
        {
            Notify("The item is the only one in its group.");
            return;
        }

        string remaining = group.MemberKeys.First(key => !string.Equals(key, member.OriginKey, StringComparison.Ordinal));
        TryRun(() =>
        {
            session.Split(member.OriginKey);
            anchorOriginKey = remaining;
            return Header(member) + " is split out of the group.";
        });
    }

    private void Decide(FindingDecision decision)
    {
        if (SelectedFinding() is not { } finding)
        {
            Notify("The group has no findings.");
            return;
        }

        TryRun(() =>
        {
            session.DecideFinding(finding.OriginKey, finding.FindingId, decision);
            return "Finding " + finding.FindingId + (decision == FindingDecision.Accepted ? " is accepted." : " is rejected.");
        });
    }

    private void ClearFinding()
    {
        if (SelectedFinding() is not { } finding)
        {
            Notify("The group has no findings.");
            return;
        }

        if (finding.Decision is null)
        {
            Notify("Finding " + finding.FindingId + " has no decision.");
            return;
        }

        TryRun(() =>
        {
            session.ClearFinding(finding.OriginKey, finding.FindingId);
            return "Finding " + finding.FindingId + " has no decision again.";
        });
    }

    private void RevealFinding()
    {
        if (SelectedFinding() is not { } finding)
        {
            Notify("The group has no findings.");
            return;
        }

        TryRun(() => session.ToggleRevealFinding(finding.OriginKey, finding.FindingId)
            ? "The value of finding " + finding.FindingId + " is revealed. Press v to mask it again."
            : "The value of finding " + finding.FindingId + " is masked.");
    }

    private void RefreshGroups()
    {
        refreshing = true;
        ShownGroups = session.Groups(Filter, search.Text);
        groupsFrame.Title = string.Create(CultureInfo.InvariantCulture, $"Groups: {FilterName(Filter)} ({ShownGroups.Count} of {session.GroupCount})");
        groupsList.SetSource(new ObservableCollection<string>(ShownGroups.Select(Line)));
        int index = anchorOriginKey is null ? -1 : FindShown(anchorOriginKey);
        if (index < 0 && ShownGroups.Count > 0)
        {
            index = 0;
        }

        groupsList.SelectedItem = index < 0 ? null : index;
        refreshing = false;
        ShowGroup(index < 0 ? null : ShownGroups[index]);
    }

    private int FindShown(string originKey)
    {
        for (int index = 0; index < ShownGroups.Count; index++)
        {
            if (ShownGroups[index].MemberKeys.Contains(originKey, StringComparer.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void GroupSelected()
    {
        if (refreshing)
        {
            return;
        }

        int? index = groupsList.SelectedItem;
        ShowGroup(index is { } selected && selected >= 0 && selected < ShownGroups.Count ? ShownGroups[selected] : null);
    }

    private void ShowGroup(ReviewGroup? group)
    {
        bool sameGroup = group is not null && string.Equals(SelectedGroup?.Key, group.Key, StringComparison.Ordinal);
        Point fieldCell = fieldsTable.Value?.SelectedCell ?? new Point(MemberColumn, 0);
        string? fieldPath = SelectedField()?.FieldPath;
        int findingRow = findingsTable.Value?.SelectedCell.Y ?? 0;
        SelectedGroup = group;
        anchorOriginKey = group?.MemberKeys[0] ?? anchorOriginKey;
        members = group is null ? [] : session.Members(group.Key);
        fields = group is null ? [] : session.Fields(group.Key);
        findings = group is null ? [] : session.Findings(group.Key);
        header.Text = group is null ? "No group matches the filter and search." : Describe(group);
        Title = Status();

        fieldsTable.Table = new RowTableSource(
            ["Field", "Decision", .. members.Select(Header)],
            [.. fields.Select(field => (IReadOnlyList<string>)[ReviewText.OneLine(field.Label), Decision(field), .. field.Values])]);
        int fieldRow = sameGroup && fieldPath is not null ? Math.Max(0, IndexOf(fieldPath)) : 0;
        int column = Math.Clamp(sameGroup ? fieldCell.X : MemberColumn, 0, Math.Max(0, MemberColumn + members.Count - 1));
        if (fields.Count > 0)
        {
            fieldsTable.Value = new TableSelection(new Point(column, Math.Min(fieldRow, fields.Count - 1)));
        }

        findingsTable.Table = new RowTableSource(
            ["Item", "Finding", "Kind", "Target", "Status", "Value"],
            [
                .. findings.Select(finding => (IReadOnlyList<string>)
                [
                    ReviewText.OneLine(finding.ItemName),
                    ReviewText.OneLine(finding.FindingId),
                    finding.Kind.ToString(),
                    ReviewText.OneLine(finding.Target),
                    ReviewText.OneLine(Status(finding)),
                    finding.Value,
                ]),
            ]);
        if (findings.Count > 0)
        {
            findingsTable.Value = new TableSelection(new Point(0, Math.Clamp(sameGroup ? findingRow : 0, 0, findings.Count - 1)));
        }
    }

    private int IndexOf(string fieldPath)
    {
        for (int index = 0; index < fields.Count; index++)
        {
            if (string.Equals(fields[index].FieldPath, fieldPath, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private ReviewField? SelectedField() =>
        fieldsTable.Value?.SelectedCell.Y is int row && row >= 0 && row < fields.Count ? fields[row] : null;

    private ReviewMember? SelectedMember() =>
        fieldsTable.Value?.SelectedCell.X is int column && column >= MemberColumn && column - MemberColumn < members.Count ? members[column - MemberColumn] : null;

    private ReviewFinding? SelectedFinding() =>
        findingsTable.Value?.SelectedCell.Y is int row && row >= 0 && row < findings.Count ? findings[row] : null;

    private string Status()
    {
        PendingDecisions pending = session.Pending;
        string state = pending.IsEmpty
            ? "nothing needs a decision"
            : string.Create(CultureInfo.InvariantCulture, $"{pending.Fields.Count} field(s) and {pending.FindingCount} finding(s) need a decision");
        return "sharedkernel-vault review: " + state + (session.IsDirty ? ", decisions not saved" : string.Empty);
    }

    private static string Describe(ReviewGroup group)
    {
        string members = string.Create(CultureInfo.InvariantCulture, $"{group.MemberKeys.Count} item(s)");
        string pending = group.IsPending
            ? string.Create(CultureInfo.InvariantCulture, $"{group.PendingFields} field(s) and {group.PendingFindings} finding(s) need a decision")
            : "nothing to decide";
        return ReviewText.OneLine(group.Name) + " | " + members + " | " + pending + (group.IsSuggested ? " | suggested match" : string.Empty);
    }

    private static string Header(ReviewMember member) =>
        ReviewText.OneLine(member.SourceFile + ": " + member.Name) + (member.IsNewest ? " (newest)" : string.Empty);

    private string Decision(ReviewField field)
    {
        if (field.Choice is { Kind: FieldChoiceKind.Member, OriginKey: { } originKey })
        {
            ReviewMember? chosen = members.FirstOrDefault(member => string.Equals(member.OriginKey, originKey, StringComparison.Ordinal));
            return "use " + ReviewText.OneLine(chosen is null ? originKey : chosen.SourceFile + ": " + chosen.Name);
        }

        if (field.Choice is { Kind: FieldChoiceKind.Empty })
        {
            return "empty";
        }

        return field.Resolution switch
        {
            ResolutionKind.Unresolved => "needs a decision",
            ResolutionKind.Newest => "newest",
            ResolutionKind.PreferredSource => "preferred source",
            ResolutionKind.OneSided => "one-sided",
            ResolutionKind.Union => "union",
            ResolutionKind.Equal => "equal",
            ResolutionKind.Decision => "decision",
            _ => string.Empty,
        };
    }

    private static string Status(ReviewFinding finding)
    {
        string decision = finding.Decision switch
        {
            FindingDecision.Accepted => "accepted",
            FindingDecision.Rejected => "rejected",
            _ when finding.IsPreAccepted => "pre-accepted",
            _ when finding.NeedsReview => "needs review",
            _ => "needs a decision",
        };
        return finding.IsApplied ? decision + ", applied"
            : finding.Problem is { } problem ? decision + ", " + problem
            : decision;
    }
}
