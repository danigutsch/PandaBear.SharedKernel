using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using SharedKernel.Terminal.Gui;
using Terminal.Gui.Input;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace SharedKernel.Packages.Tool;

/// <summary>Displays package and update details and allows confirmed edits to the selected manager.</summary>
[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Window.Dispose disposes the subviews.")]
internal sealed class PackageReviewWindow : Window
{
    private readonly PackageReviewSession _session;
    private readonly IReadOnlyList<PackageManagerKind> _managerKinds;
    private readonly ListView _managers;
    private readonly Label _heading;
    private readonly ListView _detail;
    private readonly Label _status;
    private TextField? _editor;
    private int _detailWidth;

    /// <summary>Creates the package review window from a completed observation.</summary>
    internal PackageReviewWindow(PackageReviewSession session)
    {
        _session = session;
        _managerKinds = session.Edit is null ? session.Declared.Managers : Enum.GetValues<PackageManagerKind>();
        Title = "Package review";

        var managerPane = new FrameView { Title = "Managers" };
        _managers = TerminalViews.CreateList();
        _managers.SetSource(new ObservableCollection<string>(_managerKinds.Select(PackageManagers.Id)));
        managerPane.Add(_managers);
        _heading = new Label();
        _detail = TerminalViews.CreateList();
        ListDetailLayout.Position(managerPane, _heading, _detail);

        _status = new Label { X = 0, Y = Pos.AnchorEnd(2), Width = Dim.Fill(), Height = 1, Text = "Tab switches panes." };
        var commands = new StatusBar();
        commands.Add(new Shortcut(Key.Esc, "Back", Back, string.Empty));
        if (session.Edit is not null)
        {
            commands.Add(new Shortcut(Key.F2, "Edit", BeginEdit, string.Empty));
            commands.Add(new Shortcut(Key.S.WithCtrl, "Save", ConfirmEdit, string.Empty));
        }

        Add(managerPane, _heading, _detail, _status, commands);

        _managers.ValueChanged += (_, _) => ShowSelectedManager();
        _detail.ViewportChanged += (_, _) =>
        {
            if (_detail.Viewport.Width != _detailWidth)
            {
                ReflowDetails();
            }
        };
        _managers.SelectedItem = _managerKinds.Count == 0 ? null : 0;
        ShowSelectedManager();
    }

    /// <summary>Gets the package manager whose details are displayed.</summary>
    internal PackageManagerKind? SelectedManager => _managers.SelectedItem is int index ? _managerKinds[index] : null;

    /// <summary>Gets the details text shown for the current selection.</summary>
    internal string Details { get; private set; } = string.Empty;

    /// <summary>Gets the wrapped lines in the details pane.</summary>
    internal IReadOnlyList<string> WrappedDetails { get; private set; } = [];

    /// <summary>Closes the review window.</summary>
    internal void Close() => App?.RequestStop(this);

    private void BeginEdit()
    {
        if (_editor is not null || SelectedManager is not { } manager || _session.Edit is not { } edit)
        {
            return;
        }

        _editor = new TextField
        {
            X = _detail.X,
            Y = _detail.Y,
            Width = _detail.Width,
            Height = 1,
            Text = string.Join(", ", edit.EntriesFor(manager)),
        };
        Add(_editor);
        _detail.Visible = false;
        _editor.SetFocus();
        _managers.Enabled = false;
        _status.Text = "Separate names or exact versions with commas.";
    }

    private void ConfirmEdit()
    {
        if (_editor is not { } editor || SelectedManager is not { } manager || _session.Edit is not { } edit || App is not { } app)
        {
            return;
        }

        var entries = editor.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        byte[]? confirmed;
        try
        {
            confirmed = edit.Prepare(manager, entries);
        }
        catch (InvalidOperationException exception)
        {
            _status.Text = TerminalText.OneLine(exception.Message);
            return;
        }

        if (confirmed is null)
        {
            Back();
            return;
        }

        if (TerminalDialogs.Unavailable(app, minimumWidth: 44, minimumHeight: 12) is { } unavailable)
        {
            _status.Text = unavailable;
            return;
        }

        var message = string.Create(CultureInfo.InvariantCulture,
            $"Save the {PackageManagers.Id(manager)} declaration with {entries.Length} entries in {PackagesConfiguration.RelativePath}?");
        if (TerminalDialogs.Confirm(app, "Save package set?", message))
        {
            _session.ConfirmedEdit = confirmed;
            Close();
        }
    }

    private void Back()
    {
        if (_editor is not { } editor)
        {
            Close();
            return;
        }

        _editor = null;
        Remove(editor);
        editor.Dispose();
        _detail.Visible = true;
        _managers.Enabled = true;
        _managers.SetFocus();
        _status.Text = "Tab switches panes.";
    }

    private void ShowSelectedManager()
    {
        if (SelectedManager is { } manager)
        {
            _heading.Text = PackageManagers.Id(manager);
            Details = string.Join('\n', _session.DetailsFor(manager).Select(TerminalText.OneLine));
        }
        else
        {
            _heading.Text = string.Empty;
            Details = "No packages are declared.";
        }

        ReflowDetails();
    }

    private void ReflowDetails()
    {
        _detailWidth = _detail.Viewport.Width;
        if (_detailWidth < 1)
        {
            return;
        }

        var formatter = new TextFormatter { WordWrap = true, ConstrainToWidth = _detailWidth };
        List<string> lines = [];
        foreach (var line in Details.Split('\n'))
        {
            formatter.Text = line;
            lines.AddRange(formatter.GetLines());
        }

        WrappedDetails = lines;
        _detail.SetSource(new ObservableCollection<string>(lines));
        _detail.SelectedItem = lines.Count == 0 ? null : 0;
    }
}
