using PandaBear.VaultManager.Tool.Review;
using Terminal.Gui.App;
using Terminal.Gui.Input;

namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>Drives the export password dialog headlessly.</summary>
[Collection(HeadlessTerminal.Collection)]
public sealed class ExportPasswordDialogTests
{
    private const string Password = "fixture export password";

    /// <summary>Two matching entries of at least twelve characters close the dialog and become its result.</summary>
    [Fact]
    public void Matching_entries_of_at_least_twelve_characters_are_the_result()
    {
        // Arrange
        using IApplication app = HeadlessTerminal.Start();
        using var dialog = new ExportPasswordDialog();
        SessionToken token = app.Begin(dialog).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Type(app, Password);
        HeadlessTerminal.Press(app, Key.Tab);
        HeadlessTerminal.Type(app, Password);
        HeadlessTerminal.Press(app, Key.Enter);
        app.End(token);

        // Assert
        dialog.Result.ShouldBe(Password);
        dialog.StopRequested.ShouldBeTrue();
    }

    /// <summary>An entry that is too short or does not match its confirmation keeps the dialog open, returns no result, and never echoes either entry in the problem text.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Short_or_different_entries_are_refused_and_the_dialog_stays_open(bool tooShort)
    {
        // Arrange
        string first = tooShort ? "short-pass" : Password;
        string second = tooShort ? first : "fixture other password";
        using IApplication app = HeadlessTerminal.Start();
        using var dialog = new ExportPasswordDialog();
        SessionToken token = app.Begin(dialog).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Type(app, first);
        HeadlessTerminal.Press(app, Key.Tab);
        HeadlessTerminal.Type(app, second);
        HeadlessTerminal.Press(app, Key.Enter);
        bool stopRequested = dialog.StopRequested;
        string? result = dialog.Result;
        HeadlessTerminal.Press(app, Key.Esc);
        app.End(token);

        // Assert
        stopRequested.ShouldBeFalse();
        result.ShouldBeNull();
        dialog.Problem.ShouldNotContain(first, StringComparison.Ordinal);
        dialog.Problem.ShouldNotContain(second, StringComparison.Ordinal);
    }

    /// <summary>Pressing Escape closes the dialog with no password.</summary>
    [Fact]
    public void Escape_cancels_without_a_password()
    {
        // Arrange
        using IApplication app = HeadlessTerminal.Start();
        using var dialog = new ExportPasswordDialog();
        SessionToken token = app.Begin(dialog).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Type(app, Password);
        HeadlessTerminal.Press(app, Key.Esc);
        app.End(token);

        // Assert
        dialog.StopRequested.ShouldBeTrue();
        dialog.Result.ShouldBeNull();
    }
}
