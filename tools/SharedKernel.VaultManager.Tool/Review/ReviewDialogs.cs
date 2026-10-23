using System.Collections.ObjectModel;
using System.Globalization;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace PandaBear.VaultManager.Tool.Review;

/// <summary>Modal Terminal.Gui dialogs of the review window.</summary>
/// <param name="app">Application that runs the dialogs.</param>
internal sealed class ReviewDialogs(IApplication app) : IReviewDialogs
{
    /// <summary>Fewest terminal columns the dialogs need; the password fields of the export dialog take 56.</summary>
    public const int MinimumWidth = 60;

    /// <summary>Fewest terminal rows the dialogs need.</summary>
    public const int MinimumHeight = 15;

    /// <inheritdoc/>
    /// <remarks>
    /// Terminal.Gui throws while it lays out a dialog on a terminal that is too narrow, for example one that reports a size
    /// of 0x0, and leaves the dialog behind. The size is therefore checked before a dialog opens.
    /// </remarks>
    public string? Unavailable =>
        app.Screen.Width < MinimumWidth || app.Screen.Height < MinimumHeight
            ? string.Create(CultureInfo.InvariantCulture, $"The terminal is too small for dialogs; make it at least {MinimumWidth} columns by {MinimumHeight} rows.")
            : null;

    /// <inheritdoc/>
    public QuitChoice AskQuit(bool canSave)
    {
        // The last button is the default, so Enter saves, or keeps the review open when there is nothing to save to.
        if (canSave)
        {
            return MessageBox.Query(app, "Unsaved decisions", "Save the decisions before quitting?", "Cancel", "Discard", "Save") switch
            {
                1 => QuitChoice.Discard,
                2 => QuitChoice.Save,
                _ => QuitChoice.Cancel,
            };
        }

        int? answer = MessageBox.Query(
            app,
            "Unsaved decisions",
            "There is no decisions file to save them to; start the review with --decisions <file> to keep decisions. Quit and discard them?",
            "Discard",
            "Cancel");
        return answer == 0 ? QuitChoice.Discard : QuitChoice.Cancel;
    }

    /// <inheritdoc/>
    public string? AskExportPassword()
    {
        using var dialog = new ExportPasswordDialog();
        app.Run(dialog, null);
        return dialog.Result;
    }

    /// <inheritdoc/>
    public void ShowHelp(IReadOnlyList<ReviewKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var list = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        list.SetSource(new ObservableCollection<string>(ReviewHelp.Lines(keys)));
        using var dialog = new Dialog { Title = "Keys", Width = Dim.Percent(90), Height = Dim.Percent(90) };
        dialog.Add(list);
        dialog.AddButton(new Button { Text = "Close" });
        app.Run(dialog, null);
    }

    /// <inheritdoc/>
    public void ShowError(string message) => MessageBox.ErrorQuery(app, "Problem", message, "OK");
}
