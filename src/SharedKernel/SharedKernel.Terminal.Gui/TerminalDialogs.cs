using System.Globalization;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace SharedKernel.Terminal.Gui;

/// <summary>Checks available screen space and asks for confirmation on an initialized terminal.</summary>
public static class TerminalDialogs
{
    /// <summary>Describes a terminal that is too small to open the caller's modal dialogs.</summary>
    /// <param name="app">The initialized terminal application.</param>
    /// <param name="minimumWidth">Fewest columns the dialog requires.</param>
    /// <param name="minimumHeight">Fewest rows the dialog requires.</param>
    /// <returns>A displayable reason, or <see langword="null"/> when the dialog fits.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either minimum dimension is less than one.</exception>
    public static string? Unavailable(IApplication app, int minimumWidth, int minimumHeight)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumHeight);

        return app.Screen.Width < minimumWidth || app.Screen.Height < minimumHeight
            ? string.Create(CultureInfo.InvariantCulture, $"The terminal is too small for dialogs; make it at least {minimumWidth} columns by {minimumHeight} rows.")
            : null;
    }

    /// <summary>Runs a modal Yes/Cancel prompt. Check <see cref="Unavailable"/> before opening a dialog.</summary>
    /// <param name="app">The initialized terminal application.</param>
    /// <param name="title">The prompt title.</param>
    /// <param name="message">The action and its consequence.</param>
    /// <param name="defaultToConfirm">Whether Enter chooses Yes instead of Cancel.</param>
    /// <returns>Whether the user chose Yes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is <see langword="null"/>.</exception>
    public static bool Confirm(IApplication app, string title, string message, bool defaultToConfirm = false)
    {
        ArgumentNullException.ThrowIfNull(app);

        return defaultToConfirm
            ? MessageBox.Query(app, title, message, "Cancel", "Yes") == 1
            : MessageBox.Query(app, title, message, "Yes", "Cancel") == 0;
    }
}
