using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace PandaBear.VaultManager.Tool.Review;

/// <summary>
/// Asks for the password of a new export twice, in masked fields. <see cref="Runnable{TResult}.Result"/> is the password
/// once both entries match and it has at least <see cref="PasswordReader.MinimumNewPasswordLength"/> characters, and
/// <see langword="null"/> when the user cancels. The fields are cleared after each attempt, and the password is never
/// shown.
/// </summary>
[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "The fields are subviews, which View.Dispose disposes with the dialog.")]
internal sealed class ExportPasswordDialog : Dialog<string>
{
    private readonly TextField first;
    private readonly TextField second;
    private readonly Label problem;

    /// <summary>Builds the dialog.</summary>
    public ExportPasswordDialog()
    {
        Title = "Password for the new export";
        var firstLabel = new Label { X = 0, Y = 0, Text = "Password:" };
        var secondLabel = new Label { X = 0, Y = 1, Text = "Password again:" };
        first = new TextField { X = Pos.Right(secondLabel) + 1, Y = 0, Width = 40, Secret = true };
        second = new TextField { X = Pos.Right(secondLabel) + 1, Y = 1, Width = 40, Secret = true };
        problem = new Label
        {
            X = 0,
            Y = 3,
            Width = Dim.Fill(),
            Text = string.Create(CultureInfo.InvariantCulture, $"At least {PasswordReader.MinimumNewPasswordLength} characters; it protects every secret in the file."),
        };
        Add(firstLabel, first, secondLabel, second, problem);

        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, args) =>
        {
            Result = null;
            Clear();
            RequestStop();
            args.Handled = true;
        };
        AddButton(cancel);
        AddButton(new Button { Text = "OK" });
    }

    /// <summary>Message about the last refused attempt, without the password.</summary>
    public string Problem => problem.Text;

    /// <summary>
    /// Takes the password when both entries match and it is long enough; otherwise clears both fields, says why without the
    /// password, and keeps the dialog open.
    /// </summary>
    /// <param name="args">Command arguments.</param>
    /// <returns><see langword="true"/> when the dialog stays open.</returns>
    protected override bool OnAccepting(CommandEventArgs args)
    {
        string password = first.Text;
        bool matches = string.Equals(password, second.Text, StringComparison.Ordinal);
        Clear();
        if (password.Length < PasswordReader.MinimumNewPasswordLength || !matches)
        {
            problem.Text = password.Length < PasswordReader.MinimumNewPasswordLength
                ? string.Create(CultureInfo.InvariantCulture, $"Use at least {PasswordReader.MinimumNewPasswordLength} characters; this password protects every secret in the file.")
                : "The passwords do not match. Type them again.";
            first.SetFocus();
            return true;
        }

        Result = password;
        RequestStop();
        return false;
    }

    private void Clear()
    {
        first.Text = string.Empty;
        second.Text = string.Empty;
    }
}
