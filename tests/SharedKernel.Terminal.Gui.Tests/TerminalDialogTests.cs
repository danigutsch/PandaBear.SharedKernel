using SharedKernel.Terminal.Gui.Testing;
using Terminal.Gui.App;
using Terminal.Gui.Input;

namespace SharedKernel.Terminal.Gui.Tests;

/// <summary>Tests safe confirmation defaults and fit checks on an independent terminal screen.</summary>
[Collection(HeadlessTerminal.Collection)]
public sealed class TerminalDialogTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Enter_chooses_the_configured_default(bool defaultToConfirm)
    {
        // Arrange
        using IApplication app = HeadlessTerminal.Start();
        app.Driver!.SetScreenSize(72, 20);
        bool pressed = false;
        app.Iteration += (_, _) =>
        {
            if (pressed)
            {
                app.RequestStop(app.TopRunnable);
                return;
            }

            pressed = true;
            HeadlessTerminal.Press(app, Key.Enter);
        };

        // Act
        bool confirmed = TerminalDialogs.Confirm(app, "Remove file?", "This removes the encrypted file.", defaultToConfirm);

        // Assert
        confirmed.ShouldBe(defaultToConfirm);
    }

    [Fact]
    public void A_terminal_below_the_minimum_reports_why_the_dialog_cannot_open()
    {
        // Arrange
        using IApplication app = HeadlessTerminal.Start();
        app.Driver!.SetScreenSize(42, 11);

        // Act
        string? reason = TerminalDialogs.Unavailable(app, minimumWidth: 42, minimumHeight: 12);

        // Assert
        reason.ShouldBe("The terminal is too small for dialogs; make it at least 42 columns by 12 rows.");
    }
}
