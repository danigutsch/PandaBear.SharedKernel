namespace PandaBear.VaultManager.Tool.Review;

/// <summary>Modal questions and messages of the review window. Messages hold no vault values.</summary>
internal interface IReviewDialogs
{
    /// <summary>
    /// Why the dialogs cannot be shown now, such as a terminal smaller than they need, or <see langword="null"/> when they
    /// can. The window checks it before each dialog and shows the reason on its message line instead. It holds no values.
    /// </summary>
    string? Unavailable { get; }

    /// <summary>Asks what to do with unsaved decisions before quitting.</summary>
    /// <param name="canSave">Whether the review has a decisions file to save to.</param>
    /// <returns>The choice; <see cref="QuitChoice.Cancel"/> when the question is dismissed.</returns>
    QuitChoice AskQuit(bool canSave);

    /// <summary>
    /// Asks twice, in masked fields, for the password of a new export of at least
    /// <see cref="PasswordReader.MinimumNewPasswordLength"/> characters.
    /// </summary>
    /// <returns>The password, or <see langword="null"/> when the user cancels.</returns>
    string? AskExportPassword();

    /// <summary>Asks a yes-or-no question.</summary>
    /// <param name="title">Title of the question.</param>
    /// <param name="message">Value-free question.</param>
    /// <returns><see langword="true"/> when the user agrees; <see langword="false"/> when the user declines or dismisses it.</returns>
    bool Confirm(string title, string message);

    /// <summary>Shows every key of the review.</summary>
    /// <param name="keys">Keys, in the order to show them.</param>
    void ShowHelp(IReadOnlyList<ReviewKey> keys);

    /// <summary>Shows a problem.</summary>
    /// <param name="message">Value-free message.</param>
    void ShowError(string message);
}
